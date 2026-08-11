using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Common.Interfaces;

public interface IUserRepository
{
    User? GetByEmail(string email);
    Task<User?> GetById(Guid userId);
}