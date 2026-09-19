using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Interfaces;

public interface ICommentRepository
{
    Task<List<Comment>> GetByTaskIdAsync(
        Guid taskId
    );

    Task<Comment?> GetByIdAsync(
        Guid id,
        Guid userId
    );

    Task AddAsync(Comment comment);

    void Update(Comment comment);

    void Delete(Comment comment);

    Task SaveChangesAsync();
}
