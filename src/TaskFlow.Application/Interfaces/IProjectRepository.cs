using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Interfaces;

public interface IProjectRepository
{
    Task<Project?> GetByIdAsync(
        Guid projectId,
        Guid userId
    );

    Task<List<Project>> GetAllByUserAsync(
        Guid userId
    );

    Task AddAsync(
        Project project
    );

    void Update(
        Project project
    );

    void Delete(
        Project project
    );

    Task SaveChangesAsync();
}