using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.API.Extensions;
using TaskFlow.Application.Features.AI.DTOs;
using TaskFlow.Application.Features.AI.Interfaces;

namespace TaskFlow.Api.Controllers;

[ApiController]
[Route("api/ai")]
[Authorize]
public class AiController(IAiService aiService, ILogger<AiController> logger) : ControllerBase
{
    private readonly IAiService _aiService = aiService;
    private readonly ILogger<AiController> _logger = logger;

    private Guid CurrentUserId => User.GetUserId();

    // Web defaults => camelCase cho payload SSE mà frontend đọc
    private static readonly JsonSerializerOptions SseJsonOptions = new(JsonSerializerDefaults.Web);

    // -------------------------------------------------------------------------
    // SSE (Server-Sent Events) streaming - giải thích tech:
    //
    // SSE là giao thức 1 chiều server -> client chạy trên HTTP thường. Mỗi sự kiện
    // là 1 block text kết thúc bằng 2 ký tự xuống dòng:
    //
    //     data: {"type":"chunk","text":"Xin"}\n\n
    //
    // Client (trình duyệt) mở kết nối, đọc từng block khi nó tới. Ưu điểm so với
    // WebSocket: đơn giản, đi qua HTTP/proxy bình thường, không cần bắt tay nâng cấp.
    //
    // Vài điểm bắt buộc phải làm đúng:
    //   1. Content-Type = "text/event-stream" -> trình duyệt hiểu là SSE.
    //   2. `await Response.Body.FlushAsync()` sau MỖI event. Nếu không flush,
    //      ASP.NET Core buffer lại và client chỉ nhận 1 cục lúc kết thúc -> mất
    //      hoàn toàn hiệu ứng chữ hiện dần.
    //   3. Trả về Task (không phải IActionResult) vì controller tự ghi thẳng
    //      xuống response body thay vì để framework serialize object.
    //
    // LƯU Ý lỗi: các lỗi validate/quota được ném TRƯỚC khi có byte nào được ghi,
    // nên ExceptionHandlingMiddleware vẫn trả JSON 400/404 bình thường. Còn lỗi
    // xảy ra giữa lúc stream thì client thấy thiếu event "done" -> tự xử lý.
    //
    // LƯU Ý frontend: `EventSource` của trình duyệt KHÔNG hỗ trợ POST body. Vì ta
    // cần gửi message + Authorization header, frontend nên dùng `fetch()` rồi đọc
    // response.body (ReadableStream), hoặc thư viện `@microsoft/fetch-event-source`.
    // -------------------------------------------------------------------------
    [HttpPost("chat")]
    public async Task Chat([FromBody] AiChatRequest request, CancellationToken cancellationToken)
    {
        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";

        // await foreach "kéo" lần lượt từng AiStreamEvent do service nhả ra.
        // Mỗi event đến đâu ghi + flush ngay đến đó -> realtime.
        try
        {
            await foreach (var evt in _aiService.StreamChatAsync(request, CurrentUserId, cancellationToken))
            {
                // Đóng gói event thành JSON. Dùng anonymous object nên shape gửi đi là:
                //   {"type":"session","sessionId":"..."}
                //   {"type":"chunk","text":"..."}
                //   {"type":"done","sessionId":null,"text":null}
                var payload = JsonSerializer.Serialize(new
                {
                    type = evt.Type switch
                    {
                        AiStreamEventType.Session => "session",
                        AiStreamEventType.Done => "done",
                        _ => "chunk"
                    },
                    sessionId = evt.SessionId?.ToString(),
                    text = evt.Text
                }, SseJsonOptions);

                // Đúng format SSE: "data: " + payload + 2 xuống dòng
                await Response.WriteAsync($"data: {payload}\n\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Client đóng kết nối / bấm abort -> request bị huỷ. Đây không phải lỗi.
        }
        catch (Exception ex)
        {
            // Lỗi xảy ra TRƯỚC khi stream bắt đầu (validate/quota/session sai) thì
            // response chưa ghi byte nào -> ném lại để ExceptionHandlingMiddleware
            // trả JSON 400/404/500 như bình thường.
            if (!Response.HasStarted)
                throw;

            // Lỗi GIỮA stream (Gemini trả 4xx/5xx, rớt mạng...): response đã started
            // nên không đổi được status code -> báo lỗi bằng 1 event SSE để FE biết
            // thay vì "stream đứt im lặng".
            _logger.LogError(ex, "AI chat stream failed after the response had started");

            var payload = JsonSerializer.Serialize(new
            {
                type = "error",
                message = "AI request failed. Please try again."
            }, SseJsonOptions);

            await Response.WriteAsync($"data: {payload}\n\n");
            await Response.Body.FlushAsync();
        }
    }

    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20
    )
    {
        var sessions = await _aiService.GetSessionsAsync(CurrentUserId, page, pageSize);
        return Ok(sessions);
    }

    [HttpGet("sessions/{sessionId:guid}/messages")]
    public async Task<IActionResult> GetSessionMessages(
        Guid sessionId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50
    )
    {
        var messages = await _aiService.GetSessionMessagesAsync(sessionId, CurrentUserId, page, pageSize);
        return Ok(messages);
    }

    [HttpDelete("sessions/{sessionId:guid}")]
    public async Task<IActionResult> DeleteSession(Guid sessionId)
    {
        await _aiService.DeleteSessionAsync(sessionId, CurrentUserId);
        return NoContent();
    }

    [HttpPost("parse-task")]
    public async Task<IActionResult> ParseTask([FromBody] AiTaskDraftRequest request)
    {
        var draft = await _aiService.ParseTaskDraftAsync(request.Text, CurrentUserId);
        return Ok(draft);
    }

    [HttpPost("tasks/{taskId:guid}/breakdown")]
    public async Task<IActionResult> SuggestBreakdown(Guid taskId)
    {
        var suggestions = await _aiService.SuggestTaskBreakdownAsync(taskId, CurrentUserId);
        return Ok(suggestions);
    }
}
