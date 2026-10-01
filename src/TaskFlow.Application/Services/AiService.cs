using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using TaskFlow.Application.Common;
using TaskFlow.Application.Features.AI.DTOs;
using TaskFlow.Application.Features.AI.Interfaces;
using TaskFlow.Application.Interfaces;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Features.AI.Services;

// =============================================================================
// AiService - toàn bộ business logic của tính năng chatbot/task AI.
//
// CÔNG NGHỆ DÙNG Ở ĐÂY: Microsoft.Extensions.AI (viết tắt MEAI)
// -----------------------------------------------------------------------------
// MEAI là abstraction chính thức của Microsoft cho AI. Thay vì code gọi thẳng
// SDK của Gemini/OpenAI, ta lập trình với interface `IChatClient`.
//
//   IChatClient (trong namespace Microsoft.Extensions.AI)
//     ├── GetResponseAsync(...)          -> trả về ChatResponse (ĐỢI xong mới trả)
//     └── GetStreamingResponseAsync(...) -> trả về IAsyncEnumerable<ChatResponseUpdate>
//                                          (trả dần từng mảnh ngay khi AI sinh ra)
//
// Các type kèm theo:
//   ChatMessage  : 1 message trong hội thoại, gồm (ChatRole, nội dung text).
//   ChatRole     : vai trò người nói - System (luật chơi/ngữ cảnh), User (user hỏi),
//                  Assistant (AI trả lời).
//   ChatResponse : kết quả 1 lần gọi non-streaming. Thuộc tính .Text là text đã gộp.
//   ChatResponseUpdate : 1 mảnh của câu trả lời khi streaming. .Text là text của
//                        riêng mảnh đó (có thể rỗng với update chỉ mang metadata).
//
// Vì sao dùng abstraction? Service này KHÔNG biết Gemini là gì. Muốn đổi sang
// OpenAI/Claude/Groq chỉ cần đổi 1 dòng đăng ký DI ở Infrastructure. Đây cũng là
// lý do Application chỉ reference package "Microsoft.Extensions.AI.Abstractions"
// (chỉ chứa interface) chứ không reference SDK nhà cung cấp nào.
// =============================================================================
public class AiService(
    IChatClient chatClient,
    IAiChatSessionRepository sessionRepository,
    IAiChatMessageRepository messageRepository,
    ITaskRepository taskRepository,
    IProjectRepository projectRepository,
    IMeetingRepository meetingRepository,
    ISubtaskRepository subtaskRepository,
    IOptions<AiOptions> aiOptions
) : IAiService
{
    private const int MaxMessageLength = 4000;

    // Số tin nhắn cũ gửi kèm mỗi lượt chat để AI nhớ ngữ cảnh hội thoại.
    // Không gửi hết lịch sử vì: (1) tốn token/quota, (2) context window có hạn.
    private const int HistoryWindow = 20;

    // Giới hạn dữ liệu user đưa vào system prompt (context injection) để prompt
    // không phình to bất thường khi user có hàng nghìn task.
    private const int ContextMaxTasks = 50;
    private const int ContextMaxProjects = 20;
    private const int ContextMeetingDays = 7;

    // JsonSerializerDefaults.Web => camelCase + đọc property không phân biệt hoa/thường
    // (AI hay trả "title"/"Title" lẫn lộn, dùng Web cho chắc).
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // =========================================================================
    // HÀM CHÍNH: StreamChatAsync
    //
    // Kiểu trả về `IAsyncEnumerable<AiStreamEvent>` + `yield return` = async
    // iterator. Đây là mấu chốt giúp chatbot "chữ hiện dần":
    //
    //   - Mỗi lần `yield return` một event, code CHẠM DỪNG tại đó và nhả event ra
    //     cho người gọi (controller). Controller ghi event xuống HTTP response.
    //   - Khi controller lấy event tiếp theo (await foreach), hàm này mới chạy tiếp.
    //   - Nhờ vậy ta không cần buffer cả câu trả lời trong RAM rồi mới gửi.
    //
    // `[EnumeratorCancellation] CancellationToken` = attribute báo cho compiler
    // gắn token truyền vào từ `await foreach (var x in Method(..., ct))` vào chính
    // iterator. Nhờ đó khi client ngắt kết nối, token bị huỷ và vòng lặp dừng lại,
    // không tiếp tục gọi AI một cách vô ích.
    // =========================================================================
    public async IAsyncEnumerable<AiStreamEvent> StreamChatAsync(
        AiChatRequest request,
        Guid userId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        var message = (request.Message ?? string.Empty).Trim();

        // ================== PHA 1: VALIDATE ==================
        // Những lỗi này ném ra TRƯỚC khi controller ghi byte đầu tiên xuống response,
        // nên ExceptionHandlingMiddleware vẫn kịp đổi thành JSON 400 như bình thường.
        if (message.Length == 0)
            throw new BadRequestException("Message is required");

        if (message.Length > MaxMessageLength)
            throw new BadRequestException($"Message must be at most {MaxMessageLength} characters");

        await EnsureDailyQuotaAsync(userId);

        // ================== PHA 2: SESSION ==================
        // - Có SessionId: load lại session cũ (phải kiểm tra chủ sở hữu, tránh user A
        //   đọc/ghi vào session của user B bằng cách đoán Guid).
        // - Không có: tạo session mới, lấy 60 ký tự đầu của tin nhắn làm tiêu đề.
        AiChatSession session;
        if (request.SessionId is { } sessionId)
        {
            session = await sessionRepository.GetByIdAsync(sessionId)
                ?? throw new NotFoundException("Chat session not found");

            if (session.UserId != userId)
                throw new NotFoundException("Chat session not found");
        }
        else
        {
            session = new AiChatSession
            {
                UserId = userId,
                Title = message.Length <= 60 ? message : message[..60]
            };

            session = await sessionRepository.AddAsync(session);
        }

        // Lưu tin nhắn của user NGAY (không đợi AI trả lời). Nếu AI lỗi giữa chừng
        // thì câu hỏi của user vẫn còn trong DB, và quota cũng đã tính lượt này.
        await messageRepository.AddAsync(new AiChatMessage
        {
            SessionId = session.Id,
            Role = AiChatRole.User,
            Content = message
        });

        // Event đầu tiên: báo cho client biết session nào đang được dùng.
        // Với session mới, đây là cách duy nhất client biết sessionId (để lưu lại
        // và gửi kèm ở các lượt chat sau -> nối tiếp mạch hội thoại).
        yield return new AiStreamEvent(AiStreamEventType.Session, sessionId: session.Id);

        // ================== PHA 3: GỌI AI ==================
        var chatMessages = await BuildChatMessagesAsync(session, userId);

        // await foreach = "kéo" từng mảnh text do AI sinh ra. Mỗi mảnh nhận được
        // lập tức bọc thành 1 event Chunk để controller đẩy xuống trình duyệt.
        await foreach (var text in StreamAssistantReplyAsync(session.Id, chatMessages, cancellationToken))
        {
            yield return new AiStreamEvent(AiStreamEventType.Chunk, text: text);
        }

        // Event cuối: đánh dấu stream kết thúc bình thường. Nếu client không nhận
        // được "done" thì biết là stream bị đứt giữa chừng (lỗi AI / rớt mạng).
        yield return new AiStreamEvent(AiStreamEventType.Done);
    }

    // -------------------------------------------------------------------------
    // Trái tim của streaming. Giải thích chi tiết từng tech:
    //
    // 1. `chatClient.GetStreamingResponseAsync(chatMessages, ...)` trả về
    //    IAsyncEnumerable<ChatResponseUpdate>. Mỗi ChatResponseUpdate là 1 mảnh
    //    rất nhỏ AI vừa sinh (thường 1-3 token ~ vài ký tự). `.Text` lấy phần
    //    text của mảnh đó; có mảnh không có text (chỉ metadata) -> bỏ qua.
    //
    //    Nếu dùng `GetResponseAsync` (bản không streaming) thì phải đợi AI sinh
    //    XONG CẢ CÂU mới nhận được ChatResponse.Text -> mất tính "chữ hiện dần".
    //
    // 2. Đây cũng là async iterator (IAsyncEnumerable<string>): mỗi `yield return`
    //    nhả 1 mảnh text ngay lập tức cho tầng trên, không buffer.
    //
    // 3. `partial` (StringBuilder) cộng dồn toàn bộ text để cuối cùng lưu thành
    //    1 message hoàn chỉnh của AI trong DB. Trong DB ta lưu CẢ CÂU, còn khi
    //    streaming ra ngoài ta nhả từng MẢNH - hai thứ khác nhau.
    //
    // 4. TẠI SAO try/finally mà không phải try/catch? Vì C# KHÔNG cho phép
    //    `yield return` nằm trong khối try có catch. try/finally thì hợp lệ.
    //    finally chạy trong MỌI trường hợp: stream xong bình thường, AI ném lỗi,
    //    hoặc client ngắt kết nối (CancellationToken bị huỷ) -> nhờ đó luôn lưu
    //    được phần trả lời đã nhận, không mất dữ liệu.
    // -------------------------------------------------------------------------
    private async IAsyncEnumerable<string> StreamAssistantReplyAsync(
        Guid sessionId,
        List<ChatMessage> chatMessages,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        var partial = new StringBuilder();

        try
        {
            await foreach (var update in chatClient.GetStreamingResponseAsync(
                chatMessages, cancellationToken: cancellationToken))
            {
                var text = update.Text;

                // Một số update chỉ chứa metadata (ví dụ bắt đầu/kết thúc sinh chữ,
                // thông tin model, token usage) và không có text -> bỏ qua để không
                // gửi chunk rỗng xuống client.
                if (string.IsNullOrEmpty(text))
                    continue;

                partial.Append(text);
                yield return text;
            }
        }
        finally
        {
            // Chỉ lưu khi thực sự có nội dung (tránh tạo row rỗng khi AI lỗi ngay
            // từ đầu). Không truyền CancellationToken vào AddAsync vì token có thể
            // đã bị huỷ do client ngắt kết nối - ta vẫn muốn lưu nốt phần đã nhận.
            if (partial.Length > 0)
            {
                await messageRepository.AddAsync(new AiChatMessage
                {
                    SessionId = sessionId,
                    Role = AiChatRole.Assistant,
                    Content = partial.ToString()
                });

                // Bump UpdatedAt để session này nhảy lên đầu danh sách "gần đây"
                await sessionRepository.TouchAsync(sessionId);
            }
        }
    }

    // ================== CONTEXT INJECTION (cách "dạy" AI biết dữ liệu user) =========
    // LLM không tự biết task của user. Ta phải gửi kèm dữ liệu trong mỗi request.
    // Kỹ thuật "RAG-lite": không cần vector DB, chỉ cần đọc dữ liệu HIỆN TẠI của
    // đúng user rồi nhét vào system prompt. Với app quản lý task cá nhân, lượng dữ
    // liệu mỗi user nhỏ nên cách này đơn giản mà hiệu quả.
    //
    // Cấu trúc `List<ChatMessage>` gửi cho AI (đúng như mọi LLM API yêu cầu):
    //   [0] ChatRole.System   -> "luật chơi" + toàn bộ dữ liệu hiện tại của user
    //   [1..n] ChatRole.User / ChatRole.Assistant -> lịch sử hội thoại cũ của session
    //   [cuối] ChatRole.User  -> câu hỏi mới nhất (đã lưu ở PHA 2, được load lại
    //                            trong `history` vì ta thêm sau khi lưu)
    //
    // AI đọc cả mảng theo thứ tự và sinh ra message Assistant tiếp theo. Vì gửi kèm
    // lịch sử nên AI mới "nhớ" được các lượt trước trong cùng session.
    private async Task<List<ChatMessage>> BuildChatMessagesAsync(AiChatSession session, Guid userId)
    {
        var messages = new List<ChatMessage>
        {
            // ChatRole.System = chỉ thị nền tảng, AI chỉ toàn bộ request đều đọc
            new(ChatRole.System, await BuildSystemPromptAsync(userId))
        };

        // GetLastBySessionAsync đã reverse -> thứ tự thời gian tăng dần (cũ -> mới),
        // đúng định dạng mà các LLM API mong đợi.
        var history = await messageRepository.GetLastBySessionAsync(session.Id, HistoryWindow);

        foreach (var m in history)
        {
            // Map enum lưu trong DB (AiChatRole) sang ChatRole của MEAI
            messages.Add(new ChatMessage(
                m.Role == AiChatRole.User ? ChatRole.User : ChatRole.Assistant,
                m.Content));
        }

        return messages;
    }

    private async Task<string> BuildSystemPromptAsync(Guid userId)
    {
        var now = DateTime.UtcNow;

        var projects = await projectRepository.GetAllByUserAsync(userId);
        var projectNames = projects
            .OrderByDescending(p => p.UpdatedAt)
            .Take(ContextMaxProjects)
            .Select(p => p.Name)
            .ToList();

        // Map ProjectId -> tên để hiển thị tên project cạnh task
        var projectIds = projects.ToDictionary(p => p.Id, p => p.Name);

        var tasks = (await taskRepository.GetAllByUserIdAsync(userId))
            .Where(t => !t.IsDeleted && t.Status != TaskFlow.Domain.Enums.TaskStatus.Done)
            .OrderBy(t => t.Deadline ?? DateTime.MaxValue)
            .Take(ContextMaxTasks)
            .Select(t =>
            {
                var projectName = t.ProjectId.HasValue &&
                    projectIds.TryGetValue(t.ProjectId.Value, out var name)
                        ? name
                        : null;

                var parts = new List<string> { $"status: {t.Status}", $"priority: {t.Priority}" };

                if (t.Deadline.HasValue)
                    parts.Add($"deadline: {t.Deadline:yyyy-MM-dd HH:mm} UTC");

                if (projectName != null)
                    parts.Add($"project: {projectName}");

                return $"- \"{t.Title}\" ({string.Join(", ", parts)})";
            })
            .ToList();

        var meetings = (await meetingRepository.GetAllByUserIdAsync(userId))
            .Where(m => m.StartAt >= now && m.StartAt <= now.AddDays(ContextMeetingDays))
            .OrderBy(m => m.StartAt)
            .Take(ContextMaxProjects)
            .Select(m => $"- \"{m.Title}\" at {m.StartAt:yyyy-MM-dd HH:mm} UTC")
            .ToList();

        // Raw string literal: prompt ở đây là tiếng Anh (theo convention UI text),
        // nhưng yêu cầu AI trả lời cùng ngôn ngữ user dùng (thường là tiếng Việt).
        return $"""
            You are TaskFlow Assistant, an AI helper inside a personal task management app.
            Answer in the same language the user writes in (often Vietnamese).
            Only use the data listed below; do not invent tasks, projects or meetings.
            Today is {now:yyyy-MM-dd HH:mm} UTC.
            Keep answers short and practical. When the user wants to create or split tasks,
            tell them to use the quick-create and task-breakdown features.

            === CURRENT TASKS ===
            {(tasks.Count > 0 ? string.Join("\n", tasks) : "(none)")}

            === PROJECTS ===
            {(projectNames.Count > 0 ? string.Join("\n", projectNames.Select(n => $"- {n}")) : "(none)")}

            === MEETINGS (next {ContextMeetingDays} days) ===
            {(meetings.Count > 0 ? string.Join("\n", meetings) : "(none)")}
            """;
    }

    // ================== TÍNH NĂNG TẠO TASK BẰNG NGÔN NGỮ TỰ NHIÊN ==================
    // Kỹ thuật "structured extraction": thay vì để AI trả lời văn xuôi, ta yêu cầu
    // nó trả về JSON đúng schema (mô tả ngay trong prompt). Ta parse JSON đó thành
    // DTO. Vì không chắc 100% AI luôn trả đúng nên phải parse "phòng thủ" - sai thì
    // báo lỗi chứ không crash (xem GetJsonResponseAsync / ExtractJson bên dưới).
    //
    // AI KHÔNG tự ghi vào DB. Nó chỉ trích xuất thành 1 "draft" rồi trả về cho
    // frontend. User xem lại, bấm xác nhận thì frontend mới gọi POST /api/tasks
    // (endpoint có sẵn). Cách này an toàn với prompt injection: dù AI bị "lừa"
    // cũng không thể tạo/sửa dữ liệu.
    public async Task<AiTaskDraftResponse> ParseTaskDraftAsync(string text, Guid userId)
    {
        text = (text ?? string.Empty).Trim();

        if (text.Length < 5)
            throw new BadRequestException("Please provide a longer description");

        if (text.Length > MaxMessageLength)
            throw new BadRequestException($"Text must be at most {MaxMessageLength} characters");

        await EnsureDailyQuotaAsync(userId);

        var projects = await projectRepository.GetAllByUserAsync(userId);

        // Yêu cầu AI trả JSON đúng schema. Kèm danh sách project hiện có để AI khớp
        // tên project user nhắc tới (ví dụ "project ABC") thay vì bịa tên mới.
        var prompt = $$"""
            Extract a task from the user's text and return ONLY a JSON object, no extra words, with this exact shape:
            {
              "title": "short task title (max 100 chars)",
              "description": "optional longer description or null",
              "priority": 0,
              "deadline": "ISO 8601 datetime or null",
              "projectName": "one of the project names below or null",
              "subtasks": ["optional list of 2-5 short subtask titles"]
            }
            priority must be 0 (low), 1 (medium) or 2 (high).
            Text: "{{text}}"
            Available projects: {{(projects.Count > 0 ? string.Join(", ", projects.Select(p => $"\"{p.Name}\"")) : "(none)")}}
            """;

        var draft = await GetJsonResponseAsync<AiTaskDraftJson>(prompt);

        if (draft is null || string.IsNullOrWhiteSpace(draft.Title))
            throw new BadRequestException("AI could not create a draft from this text");

        // Khớp project theo tên (không phân biệt hoa/thường). Không khớp -> để null,
        // frontend có thể cho user chọn lại.
        var matchedProject = projects.FirstOrDefault(p =>
            string.Equals(p.Name, draft.ProjectName, StringComparison.OrdinalIgnoreCase));

        return new AiTaskDraftResponse
        {
            Title = draft.Title,
            Description = draft.Description,
            // Clamp phòng trường hợp AI trả số ngoài dải (ví dụ 5)
            Priority = Math.Clamp(draft.Priority, 0, 2),
            Deadline = draft.Deadline,
            ProjectId = matchedProject?.Id,
            ProjectName = matchedProject?.Name,
            Subtasks = draft.Subtasks ?? []
        };
    }

    // ================== GỢI Ý CHIA NHỎ TASK ==================
    // Tương tự parse-task: chỉ trả về gợi ý, không tự tạo subtask. Frontend cho user
    // duyệt rồi gọi POST /api/subtasks (endpoint có sẵn).
    public async Task<List<AiSubtaskSuggestionResponse>> SuggestTaskBreakdownAsync(Guid taskId, Guid userId)
    {
        // GetByIdAsync(id, userId) đã lọc theo chủ sở hữu -> task của user khác = not found
        var task = await taskRepository.GetByIdAsync(taskId, userId)
            ?? throw new NotFoundException("Task not found");

        var existingSubtasks = await subtaskRepository.GetByTaskIdAsync(taskId);

        var prompt = $$"""
            Break the following task into 3-5 concrete, actionable subtasks.
            Do not repeat these existing subtasks: {{(existingSubtasks.Count > 0 ? string.Join(", ", existingSubtasks.Select(s => $"\"{s.Title}\"")) : "(none)")}}
            Return ONLY a JSON array, no extra words, where each item has this exact shape:
            { "title": "subtask title (max 100 chars)", "reason": "one short sentence why this step matters" }
            Task title: "{{task.Title}}"
            Task description: {{task.Description ?? "(none)"}}
            """;

        var suggestions = await GetJsonResponseAsync<List<AiSubtaskSuggestionJson>>(prompt);

        if (suggestions is null || suggestions.Count == 0)
            throw new BadRequestException("AI could not suggest subtasks for this task");

        return suggestions
            .Where(s => !string.IsNullOrWhiteSpace(s.Title))
            .Select(s => new AiSubtaskSuggestionResponse
            {
                Title = s.Title.Trim(),
                Reason = s.Reason ?? string.Empty
            })
            .ToList();
    }

    // ================== SESSION MANAGEMENT ==================
    public async Task<PagedResult<AiSessionResponse>> GetSessionsAsync(Guid userId, int page, int pageSize)
    {
        var (normalizedPage, normalizedPageSize) = PagedResult<AiSessionResponse>.Normalize(page, pageSize);

        var (items, totalCount) = await sessionRepository.GetPagedByUserAsync(
            userId,
            normalizedPage,
            normalizedPageSize
        );

        return new PagedResult<AiSessionResponse>
        {
            Items = items.Select(MapSession).ToList(),
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalCount = totalCount
        };
    }

    public async Task<PagedResult<AiChatMessageResponse>> GetSessionMessagesAsync(
        Guid sessionId,
        Guid userId,
        int page,
        int pageSize
    )
    {
        var session = await sessionRepository.GetByIdAsync(sessionId)
            ?? throw new NotFoundException("Chat session not found");

        if (session.UserId != userId)
            throw new NotFoundException("Chat session not found");

        var (normalizedPage, normalizedPageSize) = PagedResult<AiChatMessageResponse>.Normalize(page, pageSize);

        var items = await messageRepository.GetPagedBySessionAsync(sessionId, normalizedPage, normalizedPageSize);
        var totalCount = await messageRepository.CountBySessionAsync(sessionId);

        return new PagedResult<AiChatMessageResponse>
        {
            Items = items.Select(MapMessage).ToList(),
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalCount = totalCount
        };
    }

    public async Task DeleteSessionAsync(Guid sessionId, Guid userId)
    {
        var session = await sessionRepository.GetByIdAsync(sessionId)
            ?? throw new NotFoundException("Chat session not found");

        if (session.UserId != userId)
            throw new NotFoundException("Chat session not found");

        // Chỉ xoá session; toàn bộ message bị xoá theo nhờ FK OnDelete(Cascade)
        await sessionRepository.DeleteAsync(session);
    }

    // ================== QUOTA ==================
    // Bảo vệ quota free tier của Gemini (dùng chung 1 API key cho cả app):
    // đếm số tin nhắn user gửi trong ngày, vượt ngưỡng thì chặn.
    // DailyUserLimit <= 0 nghĩa là tắt giới hạn (hữu ích khi dev).
    private async Task EnsureDailyQuotaAsync(Guid userId)
    {
        if (aiOptions.Value.DailyUserLimit <= 0)
            return;

        var used = await messageRepository.CountTodayByUserAsync(userId);

        if (used >= aiOptions.Value.DailyUserLimit)
            throw new BadRequestException("Daily AI message limit reached. Try again tomorrow.");
    }

    // ================== JSON HELPERS ==================
    // Gọi AI ở chế độ KHÔNG streaming: `GetResponseAsync` trả về 1 ChatResponse
    // duy nhất sau khi AI sinh xong toàn bộ câu trả lời. `response.Text` là text
    // đã gộp (MEAI tự nối các phần text trong response.Messages).
    //
    // Ở đây ta cố tình dùng non-streaming vì parse-task/breakdown là request một
    // lần, cần nguyên khối JSON mới parse được - không cần chữ hiện dần.
    private async Task<T?> GetJsonResponseAsync<T>(string prompt)
    {
        var response = await chatClient.GetResponseAsync(
        [
            new ChatMessage(ChatRole.User, prompt)
        ]);

        var raw = ExtractJson(response.Text);

        try
        {
            // Deserialize "phòng thủ": AI có thể trả JSON thiếu field, sai kiểu...
            // JsonSerializer sẽ ném JsonException; ta bắt và trả default (null)
            // để caller chủ động ném BadRequestException thay vì lỗi 500.
            return JsonSerializer.Deserialize<T>(raw, JsonOptions);
        }
        catch (JsonException)
        {
            // AI trả về không phải JSON hợp lệ -> coi như thất bại, caller sẽ ném lỗi
            return default;
        }
    }

    // LLM hay bọc JSON trong markdown fence ```json ... ``` dù đã dặn không. Hàm này
    // cắt fence nếu có để deserialize luôn thành công.
    private static string ExtractJson(string raw)
    {
        var trimmed = raw.Trim();

        if (trimmed.StartsWith("```"))
        {
            var start = trimmed.IndexOf('\n');
            var end = trimmed.LastIndexOf("```", StringComparison.Ordinal);

            if (start >= 0 && end > start)
                trimmed = trimmed[(start + 1)..end].Trim();
        }

        return trimmed;
    }

    private static AiSessionResponse MapSession(AiChatSession session) => new()
    {
        Id = session.Id,
        Title = session.Title,
        CreatedAt = session.CreatedAt,
        UpdatedAt = session.UpdatedAt
    };

    private static AiChatMessageResponse MapMessage(AiChatMessage message) => new()
    {
        Id = message.Id,
        Role = (int)message.Role,
        Content = message.Content,
        CreatedAt = message.CreatedAt
    };

    // Các class dưới đây chỉ dùng để deserialize JSON do AI trả về (không expose ra ngoài)
    private sealed class AiTaskDraftJson
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int Priority { get; set; }
        public DateTime? Deadline { get; set; }
        public string? ProjectName { get; set; }
        public List<string>? Subtasks { get; set; }
    }

    private sealed class AiSubtaskSuggestionJson
    {
        public string Title { get; set; } = string.Empty;
        public string? Reason { get; set; }
    }
}
