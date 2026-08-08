using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Interfaces;

public interface IMeetingRepository
{
    Task<List<Meeting>> GetAllByUserIdAsync(Guid userId);
    Task<Meeting?> GetByIdAsync(Guid id, Guid userId);
    Task AddAsync(Meeting meeting);
    Task SaveChangesAsync();
    void Delete(Meeting meeting);
}