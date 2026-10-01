namespace TaskFlow.Application.Features.AI.DTOs;

public class AiChatRequest
{
    public Guid? SessionId { get; set; }

    public string Message { get; set; } = string.Empty;
}

public class AiSessionResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class AiChatMessageResponse
{
    public Guid Id { get; set; }
    public int Role { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class AiTaskDraftRequest
{
    public string Text { get; set; } = string.Empty;
}

public class AiTaskDraftResponse
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Priority { get; set; }
    public DateTime? Deadline { get; set; }
    public Guid? ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public List<string> Subtasks { get; set; } = [];
}

public class AiSubtaskSuggestionResponse
{
    public string Title { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

public enum AiStreamEventType
{
    Session = 1,
    Chunk = 2,
    Done = 3
}

// 1 sự kiện trong luồng streaming của chatbot, được service nhả ra qua
// IAsyncEnumerable rồi controller dịch thành các dòng SSE "data: {...}".
// Vì SSE là giao thức text nên cả 3 loại event dùng chung 1 class, phân biệt
// bằng Type:
//   Session -> SessionId có giá trị (báo session đang dùng để client lưu lại)
//   Chunk   -> Text có giá trị (1 mảnh chữ AI vừa sinh)
//   Done    -> không có dữ liệu (đánh dấu stream kết thúc bình thường)
public class AiStreamEvent
{
    public AiStreamEvent()
    {
    }

    public AiStreamEvent(AiStreamEventType type, Guid? sessionId = null, string? text = null)
    {
        Type = type;
        SessionId = sessionId;
        Text = text;
    }

    public AiStreamEventType Type { get; init; }
    public Guid? SessionId { get; init; }
    public string? Text { get; init; }
}
