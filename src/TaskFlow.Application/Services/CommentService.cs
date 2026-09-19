using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Features.Comments.DTOs;
using TaskFlow.Application.Features.Comments.Interfaces;
using TaskFlow.Application.Interfaces;
using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Features.Comments.Services;

public class CommentService(
    ICommentRepository commentRepository,
    ITaskRepository taskRepository,
    IUserRepository userRepository,
    IFileStorageService fileStorageService
) : ICommentService
{
    private const int MaxContentLength = 2000;

    private readonly ICommentRepository _commentRepository =
        commentRepository;

    private readonly ITaskRepository _taskRepository =
        taskRepository;

    private readonly IUserRepository _userRepository =
        userRepository;

    private readonly IFileStorageService _fileStorageService =
        fileStorageService;

    public async Task<List<CommentResponse>> GetByTaskIdAsync(
        Guid taskId,
        Guid userId
    )
    {
        await GetOwnedTaskAsync(taskId, userId);

        var comments =
            await _commentRepository.GetByTaskIdAsync(taskId);

        // Signed URL có thời hạn nên sinh 1 lần cho mỗi author rồi tái sử dụng,
        // tránh gọi S3 cho từng comment trong list.
        var avatarUrls = new Dictionary<Guid, string?>();

        foreach (var comment in comments)
        {
            if (comment.Author == null ||
                avatarUrls.ContainsKey(comment.AuthorId))
            {
                continue;
            }

            avatarUrls[comment.AuthorId] =
                await CreateAvatarUrlAsync(comment.Author.AvatarPath);
        }

        return comments
            .Select(comment => Map(
                comment,
                avatarUrls.TryGetValue(comment.AuthorId, out var url) ? url : null
            ))
            .ToList();
    }

    public async Task<CommentResponse> GetByIdAsync(
        Guid commentId,
        Guid userId
    )
    {
        var comment = await GetOwnedCommentAsync(commentId, userId);

        return Map(
            comment,
            await CreateAvatarUrlAsync(comment.Author?.AvatarPath)
        );
    }

    public async Task<CommentResponse> CreateAsync(
        Guid userId,
        CreateCommentRequest request
    )
    {
        await GetOwnedTaskAsync(request.TaskId, userId);

        var author = await _userRepository.GetByIdAsync(userId);

        if (author == null)
        {
            throw new KeyNotFoundException("User not found");
        }

        var comment = new Comment
        {
            Id = Guid.NewGuid(),
            Content = ValidateContent(request.Content),
            TaskId = request.TaskId,
            AuthorId = userId,
            Author = author
        };

        await _commentRepository.AddAsync(comment);
        await _commentRepository.SaveChangesAsync();

        return Map(
            comment,
            await CreateAvatarUrlAsync(author.AvatarPath)
        );
    }

    public async Task<CommentResponse> UpdateAsync(
        Guid commentId,
        Guid userId,
        UpdateCommentRequest request
    )
    {
        var comment = await GetOwnedCommentAsync(commentId, userId);

        comment.Content = ValidateContent(request.Content);
        comment.UpdatedAt = DateTime.UtcNow;

        _commentRepository.Update(comment);

        await _commentRepository.SaveChangesAsync();

        return Map(
            comment,
            await CreateAvatarUrlAsync(comment.Author?.AvatarPath)
        );
    }

    public async Task DeleteAsync(
        Guid commentId,
        Guid userId
    )
    {
        var comment = await GetOwnedCommentAsync(commentId, userId);

        _commentRepository.Delete(comment);

        await _commentRepository.SaveChangesAsync();
    }

    private async Task<TaskItem> GetOwnedTaskAsync(
        Guid taskId,
        Guid userId
    )
    {
        var task = await _taskRepository.GetByIdAsync(taskId, userId);

        if (task == null)
        {
            throw new KeyNotFoundException("Task not found");
        }

        return task;
    }

    private async Task<Comment> GetOwnedCommentAsync(
        Guid commentId,
        Guid userId
    )
    {
        // Scope theo AuthorId ngay ở repository: comment của user khác -> null ->
        // 404 giống hệt comment không tồn tại, không leak sự tồn tại của bản ghi.
        var comment = await _commentRepository.GetByIdAsync(commentId, userId);

        if (comment == null)
        {
            throw new KeyNotFoundException("Comment not found");
        }

        await GetOwnedTaskAsync(comment.TaskId, userId);

        return comment;
    }

    private async Task<string?> CreateAvatarUrlAsync(string? avatarPath)
    {
        if (string.IsNullOrWhiteSpace(avatarPath))
        {
            return null;
        }

        return await _fileStorageService.CreateSignedUrlAsync(avatarPath);
    }

    private static string ValidateContent(string content)
    {
        var trimmed = content?.Trim() ?? string.Empty;

        if (trimmed.Length == 0)
        {
            throw new ArgumentException(
                "Comment content is required."
            );
        }

        if (trimmed.Length > MaxContentLength)
        {
            throw new ArgumentException(
                $"Comment content must not exceed {MaxContentLength} characters."
            );
        }

        return trimmed;
    }

    private static CommentResponse Map(
        Comment comment,
        string? authorAvatarUrl
    )
    {
        return new CommentResponse
        {
            Id = comment.Id,
            TaskId = comment.TaskId,
            Content = comment.Content,
            AuthorId = comment.AuthorId,
            AuthorName = comment.Author?.FullName ?? string.Empty,
            AuthorAvatarUrl = authorAvatarUrl,
            CreatedAt = comment.CreatedAt,
            UpdatedAt = comment.UpdatedAt
        };
    }
}
