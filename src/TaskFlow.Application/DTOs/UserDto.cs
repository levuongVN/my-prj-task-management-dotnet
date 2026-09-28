namespace TaskFlow.Application.Features.Auth.DTOs;

public class UserDto
{
    public Guid Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string? AvatarUrl { get; set; }

    // null = email chưa verify -> FE hiện banner hướng dẫn verify email
    public DateTime? EmailVerifiedAt { get; set; }

    public DateTime? CreatedAt { get; set; }
}