using TaskFlow.Application.Features.Auth.DTOs;

public interface IUserService
{
    Task<UserDto> GetProfileAsync(Guid id);


    Task<UserDto> UpdateAsync(UserDto user);

    Task<bool> UpdatePasswordAsync(UserDto user, String currentPassword, String newPassword);
    Task<UserDto> UploadAvatarAsync(Guid userId, FileUploadDto file);
    Task<UserDto> DeleteAvatarAsync(Guid userId);

}