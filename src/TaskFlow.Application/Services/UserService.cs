using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Features.Auth.DTOs;
using TaskFlow.Domain.Entities;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    public UserService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }


    public async Task<UserDto> GetProfileAsync(Guid id)
    {
        var user = await _userRepository.GetByIdAsync(id);

        if (user == null)
        {
            throw new KeyNotFoundException("User not found");
        }

        return new UserDto
        {
            Id = user.Id,
            FullName = user.FullName,
            AvatarUrl = user.AvatarUrl,
            CreatedAt = user.CreatedAt,
            Email = user.Email
        };
    }

    public async Task<UserDto> UpdateAsync(UserDto user)
    {
        if (user.Id == Guid.Empty)
        {
            throw new ArgumentException("User ID is required.");
        }

        User? existingUser = await _userRepository.GetByIdAsync(user.Id);
        if (existingUser == null)
        {
            throw new KeyNotFoundException("User not found");
        }

        existingUser.AvatarUrl = user.AvatarUrl;
        existingUser.FullName = user.FullName;

        var updatedUser = await _userRepository.UpdateAsync(existingUser);

        return new UserDto
        {
            Id = updatedUser.Id,
            Email = updatedUser.Email,
            FullName = updatedUser.FullName,
            AvatarUrl = updatedUser.AvatarUrl,
            CreatedAt = updatedUser.CreatedAt
        };
    }
    public async Task<bool> UpdatePasswordAsync(UserDto user,String newPassword)
    {
        User? userCurrent = await _userRepository.GetByIdAsync(user.Id);
        if(userCurrent == null)
        {
            throw new KeyNotFoundException("User not found");
        }
        if(String.IsNullOrWhiteSpace(newPassword)|| newPassword.Length < 6) 
        {
            throw new ArgumentException("The new password is invalid");
        }
        bool isDuplicate = BCrypt.Net.BCrypt.Verify(newPassword,userCurrent.PasswordHash);
        if(isDuplicate)
        {
            throw new ArgumentException("New password must be difference current password");
        }
        userCurrent.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        var result = await _userRepository.UpdateAsync(userCurrent);
        if(result == null)
        {
            throw new Exception("Have an error");
        }
        return true;
    } 
}