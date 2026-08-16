using TaskFlow.Application.Features.Auth.DTOs;

public interface IUserService
{
    Task<UserDto> GetProfileAsync(Guid id);
    
    
    Task<UserDto> UpdateAsync(UserDto user);

    Task<bool> UpdatePasswordAsync(UserDto user, String newPassword);
    
}