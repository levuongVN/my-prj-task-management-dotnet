using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Features.Auth.DTOs;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    public UserService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }
    public async Task<UserDto> GetProfileAsync(Guid id)
    {
        var user = await _userRepository.GetById(id);

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
}