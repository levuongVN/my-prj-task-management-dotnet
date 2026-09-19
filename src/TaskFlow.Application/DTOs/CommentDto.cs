namespace TaskFlow.Application.Features.Comments.DTOs;

public class CommentResponse
{
    public Guid Id { get; set; }

    public Guid TaskId { get; set; }

    public string Content { get; set; } = string.Empty;

    public Guid AuthorId { get; set; }

    public string AuthorName { get; set; } = string.Empty;

    public string? AuthorAvatarUrl { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}

public class CreateCommentRequest
{
    public Guid TaskId { get; set; }

    public string Content { get; set; } = string.Empty;
}

public class UpdateCommentRequest
{
    public string Content { get; set; } = string.Empty;
}
