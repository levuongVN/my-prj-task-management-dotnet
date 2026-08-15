using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Common.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetByIdAsyncAsync(Guid userId);
    Task<User> AddAsync(User user); 
    Task<User> UpdateAsync(User user); 
    Task DeleteAsync(Guid userId); 
}
