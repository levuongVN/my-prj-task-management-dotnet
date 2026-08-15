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
}