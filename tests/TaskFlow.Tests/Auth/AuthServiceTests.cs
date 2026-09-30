using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using TaskFlow.Application.Common;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Features.Auth.DTOs;
using TaskFlow.Application.Features.Auth.Interfaces;
using TaskFlow.Application.Features.Auth.Services;
using TaskFlow.Application.Interfaces;
using TaskFlow.Domain.Entities;

namespace TaskFlow.Tests.Auth;

public class AuthServiceTests
{
    private readonly IJwtTokenGenerator _jwt = Substitute.For<IJwtTokenGenerator>();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IRefreshTokenRepository _refreshTokens = Substitute.For<IRefreshTokenRepository>();
    private readonly IDeviceService _devices = Substitute.For<IDeviceService>();
    private readonly IGoogleAuthProvider _google = Substitute.For<IGoogleAuthProvider>();
    private readonly IGitHubAuthProvider _github = Substitute.For<IGitHubAuthProvider>();
    private readonly IPasswordResetTokenRepository _resetTokens = Substitute.For<IPasswordResetTokenRepository>();
    private readonly IEmailVerificationTokenRepository _verifyTokens = Substitute.For<IEmailVerificationTokenRepository>();
    private readonly IEmailSender _email = Substitute.For<IEmailSender>();
    private readonly IConfiguration _config = Substitute.For<IConfiguration>();

    private AuthService Sut() => new(
        _jwt, _users, _refreshTokens, _devices, _google, _github,
        _resetTokens, _verifyTokens, _email, _config);

    private static string Sha(string raw) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));

    private static User MakeUser(string email = "a@b.com", string passwordHash = "") => new()
    {
        Email = email,
        PasswordHash = passwordHash,
        FullName = "Nguyen Van A",
    };

    private static DeviceRequest MakeDevice() => new() { Fingerprint = "fp-123" };

    public AuthServiceTests()
    {
        _config["App:FrontendUrl"].Returns("https://fe.test");
        _jwt.GenerateToken(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<Guid>()).Returns("jwt-access-token");
        _devices.UpsertDeviceAsync(Arg.Any<Guid>(), Arg.Any<DeviceRequest>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(ci => new UserDevice
            {
                Id = Guid.NewGuid(),
                UserId = ci.ArgAt<Guid>(0),
                DeviceFingerprint = "fp-123",
                IsActive = true,
            });
    }

    // ---------------- Register ----------------

    [Fact]
    public async Task Register_EmailMoi_TaoUserVaTraTokenDangNhap()
    {
        _users.GetByEmailAsync("new@mail.com").Returns((User?)null);
        _users.AddAsync(Arg.Any<User>()).Returns(ci => ci.Arg<User>());

        var res = await Sut().Register(new RegisterRequest
        {
            FullName = "New User",
            Email = "new@mail.com",
            Password = "Pass-123456",
        });

        Assert.Equal("jwt-access-token", res.AccessToken);
        Assert.Equal("new@mail.com", res.User!.Email);
        Assert.False(string.IsNullOrEmpty(res.RefreshToken.Token));
        Assert.True(res.RefreshToken.ExpiresAt > DateTime.UtcNow.AddDays(6.9));
        await _users.Received(1).AddAsync(Arg.Any<User>());
        await _refreshTokens.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task Register_EmailTonTai_NemConflict()
    {
        _users.GetByEmailAsync("dup@mail.com").Returns(MakeUser("dup@mail.com"));

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => Sut().Register(new RegisterRequest { Email = "dup@mail.com", Password = "x" }));

        Assert.Equal("Email already exists", ex.Message);
        await _users.DidNotReceive().AddAsync(Arg.Any<User>());
    }

    [Fact]
    public async Task Register_PasswordDuocHashBangBcrypt_KhongLuuPlain()
    {
        _users.GetByEmailAsync("h@mail.com").Returns((User?)null);
        User? captured = null;
        _users.AddAsync(Arg.Do<User>(u => captured = u)).Returns(ci => ci.Arg<User>());

        await Sut().Register(new RegisterRequest { Email = "h@mail.com", FullName = "H", Password = "Secret-123" });

        Assert.NotNull(captured);
        Assert.NotEqual("Secret-123", captured!.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify("Secret-123", captured.PasswordHash));
    }

    [Fact]
    public async Task Register_KemDevice_GenTokenVoiDeviceId()
    {
        _users.GetByEmailAsync("d@mail.com").Returns((User?)null);
        _users.AddAsync(Arg.Any<User>()).Returns(ci => ci.Arg<User>());

        await Sut().Register(new RegisterRequest { Email = "d@mail.com", Password = "x", Device = MakeDevice() });

        await _devices.Received(1).UpsertDeviceAsync(
            Arg.Any<Guid>(), Arg.Is<DeviceRequest>(d => d.Fingerprint == "fp-123"), null!, "Unknown", "Unknown device");
        _jwt.Received(1).GenerateToken(
            Arg.Any<Guid>(), Arg.Is("d@mail.com"), Arg.Is<Guid>(id => id != Guid.Empty));
    }

    [Fact]
    public async Task Register_KhongDevice_GenTokenVoiGuidEmpty()
    {
        _users.GetByEmailAsync("nd@mail.com").Returns((User?)null);
        _users.AddAsync(Arg.Any<User>()).Returns(ci => ci.Arg<User>());

        await Sut().Register(new RegisterRequest { Email = "nd@mail.com", Password = "x" });

        await _devices.DidNotReceive().UpsertDeviceAsync(
            Arg.Any<Guid>(), Arg.Any<DeviceRequest>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string>());
        _jwt.Received(1).GenerateToken(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Is<Guid>(id => id == Guid.Empty));
    }

    [Fact]
    public async Task Register_TaoEmailVerificationToken_Han24hVaGuiMail()
    {
        _users.GetByEmailAsync("v@mail.com").Returns((User?)null);
        _users.AddAsync(Arg.Any<User>()).Returns(ci => ci.Arg<User>());
        EmailVerificationToken? saved = null;
        await _verifyTokens.AddAsync(Arg.Do<EmailVerificationToken>(t => saved = t));

        await Sut().Register(new RegisterRequest { Email = "v@mail.com", Password = "x" });

        await _verifyTokens.Received(1).InvalidateUserTokensAsync(Arg.Any<Guid>());
        Assert.NotNull(saved);
        Assert.Equal(saved!.UserId, saved.UserId);
        Assert.InRange(saved.ExpiresAt, DateTime.UtcNow.AddHours(23.9), DateTime.UtcNow.AddHours(24.1));
        await _email.Received(1).SendAsync(
            "v@mail.com",
            Arg.Any<string>(),
            Arg.Is<string>(html => html.Contains("https://fe.test/verify-email?token=")));
    }

    // ---------------- VerifyEmail ----------------

    [Fact]
    public async Task VerifyEmail_TokenKhongTonTai_NemBadRequest()
    {
        _verifyTokens.GetByTokenHashAsync(Sha("raw-1")).Returns((EmailVerificationToken?)null);

        await Assert.ThrowsAsync<BadRequestException>(() => Sut().VerifyEmail(new VerifyEmailRequest { Token = "raw-1" }));
    }

    [Fact]
    public async Task VerifyEmail_TokenDaDung_NemBadRequest()
    {
        _verifyTokens.GetByTokenHashAsync(Sha("used")).Returns(new EmailVerificationToken { UsedAt = DateTime.UtcNow });

        await Assert.ThrowsAsync<BadRequestException>(() => Sut().VerifyEmail(new VerifyEmailRequest { Token = "used" }));
    }

    [Fact]
    public async Task VerifyEmail_TokenHetHan_NemBadRequest()
    {
        _verifyTokens.GetByTokenHashAsync(Sha("expired")).Returns(new EmailVerificationToken
        {
            ExpiresAt = DateTime.UtcNow.AddHours(-1),
        });

        await Assert.ThrowsAsync<BadRequestException>(() => Sut().VerifyEmail(new VerifyEmailRequest { Token = "expired" }));
    }

    [Fact]
    public async Task VerifyEmail_TokenHopLe_DanhDauUsedVaEmailVerified()
    {
        var user = MakeUser();
        var token = new EmailVerificationToken { User = user, ExpiresAt = DateTime.UtcNow.AddHours(1) };
        _verifyTokens.GetByTokenHashAsync(Sha("ok-token")).Returns(token);

        await Sut().VerifyEmail(new VerifyEmailRequest { Token = "ok-token" });

        Assert.NotNull(token.UsedAt);
        Assert.NotNull(user.EmailVerifiedAt);
        await _verifyTokens.Received(1).SaveChangesAsync();
    }

    // ---------------- ResendVerificationEmail ----------------

    [Fact]
    public async Task Resend_EmailKhongTonTai_ImLangKhongGuiMail()
    {
        _users.GetByEmailAsync("ghost@mail.com").Returns((User?)null);

        await Sut().ResendVerificationEmail(new ResendVerificationRequest { Email = "ghost@mail.com" });

        await _email.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
        await _verifyTokens.DidNotReceive().AddAsync(Arg.Any<EmailVerificationToken>());
    }

    [Fact]
    public async Task Resend_EmailDaVerify_ImLangKhongGuiMail()
    {
        _users.GetByEmailAsync("done@mail.com").Returns(new User { Email = "done@mail.com", EmailVerifiedAt = DateTime.UtcNow });

        await Sut().ResendVerificationEmail(new ResendVerificationRequest { Email = "done@mail.com" });

        await _email.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Resend_EmailChuaVerify_GuiLaiMailMoi()
    {
        var user = new User { Email = "pend@mail.com", FullName = "P" };
        _users.GetByEmailAsync("pend@mail.com").Returns(user);
        await _verifyTokens.AddAsync(Arg.Any<EmailVerificationToken>());

        await Sut().ResendVerificationEmail(new ResendVerificationRequest { Email = "pend@mail.com" });

        await _verifyTokens.Received(1).InvalidateUserTokensAsync(user.Id);
        await _verifyTokens.Received(1).AddAsync(Arg.Any<EmailVerificationToken>());
        await _email.Received(1).SendAsync("pend@mail.com", Arg.Any<string>(), Arg.Is<string>(h => h.Contains("/verify-email?token=")));
    }

    // ---------------- Login ----------------

    [Fact]
    public async Task Login_EmailKhongTonTai_NemUnauthorized()
    {
        _users.GetByEmailAsync("no@mail.com").Returns((User?)null);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => Sut().Login(new LoginRequest { Email = "no@mail.com", Password = "any" }));

        Assert.Equal("Invalid email or password", ex.Message);
    }

    [Fact]
    public async Task Login_SaiPassword_NemUnauthorizedCungMessage()
    {
        _users.GetByEmailAsync("a@b.com").Returns(MakeUser(passwordHash: BCrypt.Net.BCrypt.HashPassword("Right-Pass-1")));

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => Sut().Login(new LoginRequest { Email = "a@b.com", Password = "Wrong-Pass" }));

        Assert.Equal("Invalid email or password", ex.Message);
    }

    [Fact]
    public async Task Login_DungPassword_TraTokenVaUser()
    {
        var user = MakeUser("a@b.com", BCrypt.Net.BCrypt.HashPassword("Right-Pass-1"));
        _users.GetByEmailAsync("a@b.com").Returns(user);

        var res = await Sut().Login(new LoginRequest { Email = "a@b.com", Password = "Right-Pass-1" });

        Assert.Equal("jwt-access-token", res.AccessToken);
        Assert.Equal(user.Id, res.User!.Id);
        Assert.Equal(user.FullName, res.User.FullName);
        _jwt.Received(1).GenerateToken(user.Id, user.Email, Guid.Empty);
    }

    [Fact]
    public async Task Login_KemDevice_UpsertDeviceVaDungDeviceId()
    {
        var user = MakeUser("a@b.com", BCrypt.Net.BCrypt.HashPassword("Right-Pass-1"));
        _users.GetByEmailAsync("a@b.com").Returns(user);

        await Sut().Login(new LoginRequest { Email = "a@b.com", Password = "Right-Pass-1", Device = MakeDevice() });

        await _devices.Received(1).UpsertDeviceAsync(
            user.Id, Arg.Any<DeviceRequest>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string>());
        _jwt.Received(1).GenerateToken(
            Arg.Is(user.Id), Arg.Is(user.Email), Arg.Is<Guid>(id => id != Guid.Empty));
    }

    // ---------------- RefreshToken ----------------

    [Fact]
    public async Task Refresh_TokenKhongTonTai_NemUnauthorized()
    {
        _refreshTokens.GetByTokenAsync("missing").Returns((RefreshToken?)null);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => Sut().RefreshToken(new RefreshTokenRequest { RefreshToken = "missing" }));

        Assert.Equal("Refresh token not found", ex.Message);
    }

    [Fact]
    public async Task Refresh_TokenBiRevoked_NemUnauthorized()
    {
        _refreshTokens.GetByTokenAsync("revoked").Returns(new RefreshToken
        {
            IsRevoked = true,
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            User = MakeUser(),
        });

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => Sut().RefreshToken(new RefreshTokenRequest { RefreshToken = "revoked" }));

        Assert.Equal("Refresh token is expired or revoked", ex.Message);
    }

    [Fact]
    public async Task Refresh_TokenHetHan_NemUnauthorized()
    {
        _refreshTokens.GetByTokenAsync("old").Returns(new RefreshToken
        {
            ExpiresAt = DateTime.UtcNow.AddMinutes(-5),
            User = MakeUser(),
        });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => Sut().RefreshToken(new RefreshTokenRequest { RefreshToken = "old" }));
    }

    [Fact]
    public async Task Refresh_DeviceDaLogout_NemUnauthorized()
    {
        _refreshTokens.GetByTokenAsync("dead-device").Returns(new RefreshToken
        {
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            User = MakeUser(),
            UserDevice = new UserDevice { IsActive = false },
        });

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => Sut().RefreshToken(new RefreshTokenRequest { RefreshToken = "dead-device" }));

        Assert.Equal("Device has been logged out", ex.Message);
    }

    [Fact]
    public async Task Refresh_DeviceActive_CapNhatLastActiveVaTraTokenMoi()
    {
        var device = new UserDevice { IsActive = true, LastActiveAt = DateTime.UtcNow.AddHours(-2) };
        var stored = new RefreshToken
        {
            Token = "still-valid",
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            User = MakeUser(),
            UserDevice = device,
            UserDeviceId = device.Id,
        };
        _refreshTokens.GetByTokenAsync("still-valid").Returns(stored);

        var res = await Sut().RefreshToken(new RefreshTokenRequest { RefreshToken = "still-valid" });

        Assert.Equal("jwt-access-token", res.AccessToken);
        Assert.Equal("still-valid", res.RefreshToken.Token);
        Assert.True(device.LastActiveAt > DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task Refresh_TokenKhongGanDevice_VanTraTokenVoiGuidEmpty()
    {
        var stored = new RefreshToken
        {
            Token = "no-device",
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            User = MakeUser(),
            UserDevice = null,
            UserDeviceId = null,
        };
        _refreshTokens.GetByTokenAsync("no-device").Returns(stored);

        var res = await Sut().RefreshToken(new RefreshTokenRequest { RefreshToken = "no-device" });

        Assert.Equal("jwt-access-token", res.AccessToken);
        _jwt.Received(1).GenerateToken(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Is<Guid>(id => id == Guid.Empty));
    }

    // ---------------- Logout ----------------

    [Fact]
    public async Task Logout_TokenKhongTonTai_IdempotentKhongLoi()
    {
        _refreshTokens.GetByTokenAsync("ghost").Returns((RefreshToken?)null);

        await Sut().Logout(new LogoutRequest { RefreshToken = "ghost" });

        await _refreshTokens.DidNotReceive().SaveChangesAsync();
        await _devices.DidNotReceive().RevokeDeviceAsync(Arg.Any<Guid>(), Arg.Any<Guid>());
    }

    [Fact]
    public async Task Logout_TokenDaRevoked_IdempotentKhongRevokeLanNua()
    {
        _refreshTokens.GetByTokenAsync("already").Returns(new RefreshToken { IsRevoked = true });

        await Sut().Logout(new LogoutRequest { RefreshToken = "already" });

        await _devices.DidNotReceive().RevokeDeviceAsync(Arg.Any<Guid>(), Arg.Any<Guid>());
        await _refreshTokens.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task Logout_TokenHopLeKhongDevice_RevocaToken()
    {
        var stored = new RefreshToken { Token = "t1", UserDeviceId = null, User = MakeUser() };
        _refreshTokens.GetByTokenAsync("t1").Returns(stored);

        await Sut().Logout(new LogoutRequest { RefreshToken = "t1" });

        Assert.True(stored.IsRevoked);
        await _refreshTokens.Received(1).SaveChangesAsync();
        await _devices.DidNotReceive().RevokeDeviceAsync(Arg.Any<Guid>(), Arg.Any<Guid>());
    }

    [Fact]
    public async Task Logout_TokenCoDevice_RevokeToanBoDevice()
    {
        var device = new UserDevice { Id = Guid.NewGuid() };
        var user = MakeUser();
        var stored = new RefreshToken { Token = "t2", User = user, UserDevice = device, UserDeviceId = device.Id };
        _refreshTokens.GetByTokenAsync("t2").Returns(stored);

        await Sut().Logout(new LogoutRequest { RefreshToken = "t2" });

        Assert.True(stored.IsRevoked);
        await _devices.Received(1).RevokeDeviceAsync(user.Id, device.Id);
        await _refreshTokens.Received(1).SaveChangesAsync();
    }

    // ---------------- ForgotPassword ----------------

    [Fact]
    public async Task Forgot_EmailKhongTonTai_Tra200NhungKhongGuiMail()
    {
        _users.GetByEmailAsync("ghost@mail.com").Returns((User?)null);

        await Sut().ForgotPassword(new ForgotPasswordRequest { Email = "ghost@mail.com" });

        await _email.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
        await _resetTokens.DidNotReceive().AddAsync(Arg.Any<PasswordResetToken>());
    }

    [Fact]
    public async Task Forgot_EmailTonTai_TaoTokenHan15PhutVaGuiLink()
    {
        var user = MakeUser("reset@mail.com");
        _users.GetByEmailAsync("reset@mail.com").Returns(user);
        PasswordResetToken? saved = null;
        await _resetTokens.AddAsync(Arg.Do<PasswordResetToken>(t => saved = t));

        await Sut().ForgotPassword(new ForgotPasswordRequest { Email = "reset@mail.com" });

        await _resetTokens.Received(1).InvalidateUserTokensAsync(user.Id);
        Assert.NotNull(saved);
        Assert.InRange(saved!.ExpiresAt, DateTime.UtcNow.AddMinutes(14.9), DateTime.UtcNow.AddMinutes(15.1));
        await _email.Received(1).SendAsync(
            "reset@mail.com", Arg.Any<string>(), Arg.Is<string>(h => h.Contains("https://fe.test/reset-password?token=")));
    }

    // ---------------- ResetPassword ----------------

    [Fact]
    public async Task Reset_TokenKhongTonTai_NemBadRequest()
    {
        _resetTokens.GetByTokenHashAsync(Sha("nope")).Returns((PasswordResetToken?)null);

        await Assert.ThrowsAsync<BadRequestException>(
            () => Sut().ResetPassword(new ResetPasswordRequest { Token = "nope", NewPassword = "New-1" }));
    }

    [Fact]
    public async Task Reset_TokenHetHan_NemBadRequest()
    {
        _resetTokens.GetByTokenHashAsync(Sha("old")).Returns(new PasswordResetToken
        {
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1),
            User = MakeUser(),
        });

        await Assert.ThrowsAsync<BadRequestException>(
            () => Sut().ResetPassword(new ResetPasswordRequest { Token = "old", NewPassword = "New-1" }));
    }

    [Fact]
    public async Task Reset_TokenDaDung_NemBadRequest()
    {
        _resetTokens.GetByTokenHashAsync(Sha("used")).Returns(new PasswordResetToken
        {
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            UsedAt = DateTime.UtcNow.AddMinutes(-2),
            User = MakeUser(),
        });

        await Assert.ThrowsAsync<BadRequestException>(
            () => Sut().ResetPassword(new ResetPasswordRequest { Token = "used", NewPassword = "New-1" }));
    }

    [Fact]
    public async Task Reset_TokenHopLe_DoiPasswordVaRevokeToanBoSession()
    {
        var user = MakeUser();
        var token = new PasswordResetToken { User = user, ExpiresAt = DateTime.UtcNow.AddMinutes(10) };
        _resetTokens.GetByTokenHashAsync(Sha("good")).Returns(token);

        await Sut().ResetPassword(new ResetPasswordRequest { Token = "good", NewPassword = "Brand-New-9" });

        Assert.True(BCrypt.Net.BCrypt.Verify("Brand-New-9", user.PasswordHash));
        Assert.NotNull(token.UsedAt);
        await _refreshTokens.Received(1).RevokeByUserAsync(user.Id);
        await _resetTokens.Received(1).SaveChangesAsync();
    }

    // ---------------- OAuth: Google / GitHub ----------------

    [Fact]
    public async Task Google_EmailDaCoAccount_DungLaiUserCu()
    {
        var existing = MakeUser("g@gmail.com", "hash");
        _google.ValidateIdTokenAsync("id-token").Returns(new ExternalUserInfo { Email = "g@gmail.com", FullName = "G User" });
        _users.GetByEmailAsync("g@gmail.com").Returns(existing);

        var res = await Sut().LoginWithGoogle(new GoogleLoginRequest { IdToken = "id-token" });

        Assert.Equal(existing.Id, res.User!.Id);
        await _users.DidNotReceive().AddAsync(Arg.Any<User>());
    }

    [Fact]
    public async Task Google_EmailMoi_AutoTaoUserEmailDaVerifyVaHashRong()
    {
        _google.ValidateIdTokenAsync("id-token").Returns(new ExternalUserInfo { Email = "new@gmail.com", FullName = "New G" });
        _users.GetByEmailAsync("new@gmail.com").Returns((User?)null);
        User? created = null;
        _users.AddAsync(Arg.Do<User>(u => created = u)).Returns(ci => ci.Arg<User>());

        var res = await Sut().LoginWithGoogle(new GoogleLoginRequest { IdToken = "id-token" });

        Assert.NotNull(created);
        Assert.Equal(string.Empty, created!.PasswordHash);
        Assert.NotNull(created.EmailVerifiedAt);
        Assert.Equal("New G", created.FullName);
        Assert.Null(created.AvatarPath);
        await _email.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
        Assert.Equal("new@gmail.com", res.User!.Email);
    }

    [Fact]
    public async Task Google_ProviderKhongTraName_FallbackVeEmail()
    {
        _google.ValidateIdTokenAsync("id-token").Returns(new ExternalUserInfo { Email = "noname@gmail.com", FullName = null });
        _users.GetByEmailAsync("noname@gmail.com").Returns((User?)null);
        User? created = null;
        _users.AddAsync(Arg.Do<User>(u => created = u)).Returns(ci => ci.Arg<User>());

        await Sut().LoginWithGoogle(new GoogleLoginRequest { IdToken = "id-token" });

        Assert.Equal("noname@gmail.com", created!.FullName);
    }

    [Fact]
    public async Task GitHub_CodeHopLe_LoginVaoAccountCuHoacTaoMoi()
    {
        var existing = MakeUser("gh@users.noreply.github.com");
        _github.ExchangeCodeAsync("code-1").Returns(new ExternalUserInfo { Email = "gh@users.noreply.github.com" });
        _users.GetByEmailAsync("gh@users.noreply.github.com").Returns(existing);

        var res = await Sut().LoginWithGitHub(new GithubLoginRequest { Code = "code-1" });

        Assert.Equal(existing.Id, res.User!.Id);
        await _users.DidNotReceive().AddAsync(Arg.Any<User>());
    }

    [Fact]
    public async Task GitHub_EmailMoi_TaoUserMoi()
    {
        _github.ExchangeCodeAsync("code-2").Returns(new ExternalUserInfo { Email = "fresh@users.noreply.github.com", FullName = "GH Fresh" });
        _users.GetByEmailAsync("fresh@users.noreply.github.com").Returns((User?)null);
        User? created = null;
        _users.AddAsync(Arg.Do<User>(u => created = u)).Returns(ci => ci.Arg<User>());

        await Sut().LoginWithGitHub(new GithubLoginRequest { Code = "code-2" });

        Assert.NotNull(created);
        Assert.Equal("GH Fresh", created!.FullName);
        Assert.NotNull(created.EmailVerifiedAt);
    }
}
