using TaskFlow.Application.Features.Auth.DTOs;

public interface IUserService
{
    Task<UserDto> GetProfileAsync(Guid id);
    
} 