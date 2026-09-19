using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Interfaces;

public interface ISubtaskRepository
{
    Task<List<SubtaskItem>> GetByTaskIdAsync(Guid taskId);

    Task<SubtaskItem?> GetByIdAsync(Guid id);

    Task AddAsync(SubtaskItem subtask);

    void Update(SubtaskItem subtask);

    void Delete(SubtaskItem subtask);

    Task SaveChangesAsync();
}
