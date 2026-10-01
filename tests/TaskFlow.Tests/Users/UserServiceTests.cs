using NSubstitute;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.DTOs;
using TaskFlow.Application.Features.Auth.DTOs;
using TaskFlow.Application.Interfaces;
using TaskFlow.Application.Services;
using TaskFlow.Domain.Entities;

namespace TaskFlow.Tests.Users;

public class UserServiceTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IFileStorageService _storage = Substitute.For<IFileStorageService>();

    private UserService Sut() => new(_users, _storage);

    private static readonly Guid UserId = Guid.NewGuid();

    private static User MakeUser(string avatarPath = "") => new()
    {
        Id = UserId,
        Email = "a@b.com",
        FullName = "Old Name",
        PasswordHash = BCrypt.Net.BCrypt.HashPassword("Current-1"),
        AvatarPath = string.IsNullOrWhiteSpace(avatarPath) ? null : avatarPath,
    };

    private static FileUploadDto MakeFile(long length = 1024, string contentType = "image/png") => new()
    {
        FileName = "avatar.png",
        ContentType = contentType,
        Length = length,
    };

    [Fact]
    public async Task GetProfile_KhongTonTai_NemKeyNotFound()
    {
        _users.GetByIdAsync(Arg.Any<Guid>()).Returns((User?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => Sut().GetProfileAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetProfile_CoAvatar_TraSignedUrl()
    {
        _users.GetByIdAsync(UserId).Returns(MakeUser("avatars/me.png"));
        _storage.CreateSignedUrlAsync("avatars/me.png").Returns("https://signed/me");

        var res = await Sut().GetProfileAsync(UserId);

        Assert.Equal("https://signed/me", res.AvatarUrl);
        Assert.Equal("a@b.com", res.Email);
    }

    [Fact]
    public async Task GetProfile_KhongAvatar_UrlNullKhongGoiStorage()
    {
        _users.GetByIdAsync(UserId).Returns(MakeUser());

        var res = await Sut().GetProfileAsync(UserId);

        Assert.Null(res.AvatarUrl);
        await _storage.DidNotReceive().CreateSignedUrlAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task Update_IdRong_NemArgument()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Sut().UpdateAsync(new UserDto { Id = Guid.Empty }));
    }

    [Fact]
    public async Task Update_KhongTonTai_NemKeyNotFound()
    {
        _users.GetByIdAsync(Arg.Any<Guid>()).Returns((User?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => Sut().UpdateAsync(new UserDto { Id = UserId }));
    }

    [Fact]
    public async Task Update_HopLe_DoiFullName()
    {
        var user = MakeUser();
        _users.GetByIdAsync(UserId).Returns(user);
        _users.UpdateAsync(Arg.Any<User>()).Returns(ci => ci.Arg<User>());

        var res = await Sut().UpdateAsync(new UserDto { Id = UserId, FullName = "New Name" });

        Assert.Equal("New Name", res.FullName);
        Assert.Equal("New Name", user.FullName);
        await _users.Received(1).UpdateAsync(user);
    }

    [Fact]
    public async Task UpdatePassword_UserKhongTonTai_NemKeyNotFound()
    {
        _users.GetByIdAsync(Arg.Any<Guid>()).Returns((User?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => Sut().UpdatePasswordAsync(new UserDto { Id = UserId }, "Current-1", "NewPass-1"));
    }

    [Fact]
    public async Task UpdatePassword_CurrentPasswordQuaNgan_NemArgument()
    {
        _users.GetByIdAsync(UserId).Returns(MakeUser());

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => Sut().UpdatePasswordAsync(new UserDto { Id = UserId }, "12345", "NewPass-1"));

        Assert.Equal("The current password is invalid", ex.Message);
    }

    [Fact]
    public async Task UpdatePassword_CurrentPasswordSai_NemArgument()
    {
        _users.GetByIdAsync(UserId).Returns(MakeUser());

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => Sut().UpdatePasswordAsync(new UserDto { Id = UserId }, "Wrong-Pass", "NewPass-1"));

        Assert.Equal("The current password is not true!", ex.Message);
    }

    [Fact]
    public async Task UpdatePassword_NewPasswordQuaNgan_NemArgument()
    {
        _users.GetByIdAsync(UserId).Returns(MakeUser());

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => Sut().UpdatePasswordAsync(new UserDto { Id = UserId }, "Current-1", "12345"));

        Assert.Equal("The new password is invalid", ex.Message);
    }

    [Fact]
    public async Task UpdatePassword_NewPasswordTrungCu_NemArgument()
    {
        _users.GetByIdAsync(UserId).Returns(MakeUser());

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => Sut().UpdatePasswordAsync(new UserDto { Id = UserId }, "Current-1", "Current-1"));

        Assert.Equal("New password must be difference current password", ex.Message);
    }

    [Fact]
    public async Task UpdatePassword_HopLe_HashMoiVerifyDuoc()
    {
        var user = MakeUser();
        _users.GetByIdAsync(UserId).Returns(user);
        _users.UpdateAsync(Arg.Any<User>()).Returns(ci => ci.Arg<User>());

        var res = await Sut().UpdatePasswordAsync(new UserDto { Id = UserId }, "Current-1", "Brand-New-7");

        Assert.True(res);
        Assert.True(BCrypt.Net.BCrypt.Verify("Brand-New-7", user.PasswordHash));
        await _users.Received(1).UpdateAsync(user);
    }

    [Fact]
    public async Task UploadAvatar_FileRong_NemArgument()
    {
        _users.GetByIdAsync(UserId).Returns(MakeUser());

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => Sut().UploadAvatarAsync(UserId, MakeFile(length: 0)));

        Assert.Equal("Avatar file is required.", ex.Message);
    }

    [Fact]
    public async Task UploadAvatar_Qua1MB_NemArgument()
    {
        _users.GetByIdAsync(UserId).Returns(MakeUser());

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => Sut().UploadAvatarAsync(UserId, MakeFile(length: 1024 * 1024 + 1)));

        Assert.Equal("Avatar must not exceed 1MB.", ex.Message);
    }

    [Fact]
    public async Task UploadAvatar_SaiContentType_NemArgument()
    {
        _users.GetByIdAsync(UserId).Returns(MakeUser());

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => Sut().UploadAvatarAsync(UserId, MakeFile(contentType: "image/gif")));

        Assert.Equal("Only JPEG, PNG and WEBP images are allowed.", ex.Message);
    }

    [Fact]
    public async Task UploadAvatar_HopLe_LuuPathVaTraSignedUrl()
    {
        var user = MakeUser();
        _users.GetByIdAsync(UserId).Returns(user);
        _users.UpdateAsync(Arg.Any<User>()).Returns(ci => ci.Arg<User>());
        _storage.UploadAvatarAsync(UserId, Arg.Any<FileUploadDto>(), Arg.Any<CancellationToken>()).Returns("avatars/new.png");
        _storage.CreateSignedUrlAsync("avatars/new.png").Returns("https://signed/new");

        var res = await Sut().UploadAvatarAsync(UserId, MakeFile(contentType: "IMAGE/JPEG"));

        Assert.Equal("https://signed/new", res.AvatarUrl);
        Assert.Equal("avatars/new.png", user.AvatarPath);
    }

    [Fact]
    public async Task DeleteAvatar_KhongTonTai_NemKeyNotFound()
    {
        _users.GetByIdAsync(Arg.Any<Guid>()).Returns((User?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => Sut().DeleteAvatarAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task DeleteAvatar_CoAvatar_XoaFileVaPathNull()
    {
        var user = MakeUser("avatars/old.png");
        _users.GetByIdAsync(UserId).Returns(user);
        _users.UpdateAsync(Arg.Any<User>()).Returns(ci => ci.Arg<User>());

        var res = await Sut().DeleteAvatarAsync(UserId);

        Assert.Null(res.AvatarUrl);
        Assert.Null(user.AvatarPath);
        await _storage.Received(1).DeleteAsync("avatars/old.png");
    }

    [Fact]
    public async Task DeleteAvatar_KhongCoAvatar_KhongGoiStorage()
    {
        var user = MakeUser();
        _users.GetByIdAsync(UserId).Returns(user);
        _users.UpdateAsync(Arg.Any<User>()).Returns(ci => ci.Arg<User>());

        await Sut().DeleteAvatarAsync(UserId);

        await _storage.DidNotReceive().DeleteAsync(Arg.Any<string>());
    }
}
