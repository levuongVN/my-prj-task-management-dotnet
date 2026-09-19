using TaskFlow.Application.Features.Comments.DTOs;

namespace TaskFlow.Application.Features.Comments.Interfaces;

public interface ICommentService
{
    Task<List<CommentResponse>> GetByTaskIdAsync(
        Guid taskId,
        Guid userId
    );

    Task<CommentResponse> GetByIdAsync(
        Guid commentId,
        Guid userId
    );

    Task<CommentResponse> CreateAsync(
        Guid userId,
        CreateCommentRequest request
    );

    Task<CommentResponse> UpdateAsync(
        Guid commentId,
        Guid userId,
        UpdateCommentRequest request
    );

    Task DeleteAsync(
        Guid commentId,
        Guid userId
    );
}
