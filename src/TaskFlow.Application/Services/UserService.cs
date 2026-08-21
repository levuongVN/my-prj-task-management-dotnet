using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Features.Auth.DTOs;
using TaskFlow.Domain.Entities;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IFileStorageService _fileStorageService;
    public UserService(IUserRepository userRepository, IFileStorageService fileStorageService)
    {
        _userRepository = userRepository;
        _fileStorageService = fileStorageService;
    }


    public async Task<UserDto> GetProfileAsync(Guid id)
    {
        var user = await _userRepository.GetByIdAsync(id);

        if (user == null)
        {
            throw new KeyNotFoundException("User not found");
        }

        string? avatarUrl = null;

        if (!string.IsNullOrWhiteSpace(user.AvatarPath))
        {
            avatarUrl = await _fileStorageService.CreateSignedUrlAsync(
                user.AvatarPath
            );
        }

        return new UserDto
        {
            Id = user.Id,
            FullName = user.FullName,
            AvatarUrl = avatarUrl,
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

        var existingUser = await _userRepository.GetByIdAsync(user.Id);

        if (existingUser == null)
        {
            throw new KeyNotFoundException("User not found");
        }

        existingUser.FullName = user.FullName;

        var updatedUser = await _userRepository.UpdateAsync(existingUser);

        string? avatarUrl = null;

        if (!string.IsNullOrWhiteSpace(updatedUser.AvatarPath))
        {
            avatarUrl = await _fileStorageService.CreateSignedUrlAsync(
                updatedUser.AvatarPath
            );
        }

        return new UserDto
        {
            Id = updatedUser.Id,
            Email = updatedUser.Email,
            FullName = updatedUser.FullName,
            AvatarUrl = avatarUrl,
            CreatedAt = updatedUser.CreatedAt
        };
    }
    public async Task<bool> UpdatePasswordAsync(UserDto user,String currentPassword,String newPassword)
    {
        User? userCurrent = await _userRepository.GetByIdAsync(user.Id);
        if (userCurrent == null)
        {
            throw new KeyNotFoundException("User not found");
        }
        if(String.IsNullOrWhiteSpace(currentPassword) || currentPassword.Length < 6)
        {
            throw new ArgumentException("The current password is invalid");
        }
        bool isVerifyPassword = BCrypt.Net.BCrypt.Verify(currentPassword,userCurrent.PasswordHash);
        if (!isVerifyPassword)
        {
            throw new ArgumentException("The current password is not true!");
        }
        if (String.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
        {
            throw new ArgumentException("The new password is invalid");
        }
        bool isDuplicate = newPassword.Equals(currentPassword);
        if (isDuplicate)
        {
            throw new ArgumentException("New password must be difference current password");
        }
        userCurrent.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        var result = await _userRepository.UpdateAsync(userCurrent);
        if (result == null)
        {
            throw new Exception("Have an error");
        }
        return true;
    }

    public async Task<UserDto> UploadAvatarAsync(
    Guid userId,
    FileUploadDto file)
    {
        var user = await _userRepository.GetByIdAsync(userId);

        if (user == null)
        {
            throw new KeyNotFoundException("User not found");
        }

        ValidateAvatar(file);

        // Upload lên Supabase
        var avatarPath = await _fileStorageService.UploadAvatarAsync(
                userId,
                file
            );

        // Lưu PATH vào DB
        user.AvatarPath = avatarPath;

        var updatedUser = await _userRepository.UpdateAsync(user);

        // Sinh URL tạm để FE có thể hiển thị ngay
        var avatarUrl = await _fileStorageService.CreateSignedUrlAsync(
                updatedUser.AvatarPath!
            );

        return new UserDto
        {
            Id = updatedUser.Id,
            Email = updatedUser.Email,
            FullName = updatedUser.FullName,
            AvatarUrl = avatarUrl,
            CreatedAt = updatedUser.CreatedAt
        };
    }
    public async Task<UserDto> DeleteAvatarAsync(Guid userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);

        if (user == null)
        {
            throw new KeyNotFoundException("User not found");
        }

        if (!string.IsNullOrWhiteSpace(user.AvatarPath))
        {
            // Xóa file thật trên Supabase
            await _fileStorageService.DeleteAsync(
                user.AvatarPath
            );
        }

        // Xóa path trong DB
        user.AvatarPath = null;

        var updatedUser = await _userRepository.UpdateAsync(user);

        return new UserDto
        {
            Id = updatedUser.Id,
            Email = updatedUser.Email,
            FullName = updatedUser.FullName,
            AvatarUrl = null,
            CreatedAt = updatedUser.CreatedAt
        };
    }

    private static void ValidateAvatar(
        FileUploadDto file)
    {
        const long maxFileSize = 1024 * 1024; // 1 MB

        if (file.Length <= 0)
        {
            throw new ArgumentException(
                "Avatar file is required."
            );
        }

        if (file.Length > maxFileSize)
        {
            throw new ArgumentException(
                "Avatar must not exceed 1MB."
            );
        }

        string[] allowedContentTypes ={
            "image/jpeg",
            "image/png",
            "image/webp"
        };

        if (!allowedContentTypes.Contains(
            file.ContentType.ToLowerInvariant()))
        {
            throw new ArgumentException(
                "Only JPEG, PNG and WEBP images are allowed."
            );
        }
    }
}