using TaskFlow.Application.Features.Auth.DTOs;

public interface IUserService
{
    Task<UserDto> GetProfileAsync(Guid id);
    Task<UserDto> GetByIdAsync(Guid id);
    Task<UserDto> AddAsync(UserDto user);
    Task<UserDto> UpdateAsync(UserDto user);
    Task DeleteAsync(Guid id);
}