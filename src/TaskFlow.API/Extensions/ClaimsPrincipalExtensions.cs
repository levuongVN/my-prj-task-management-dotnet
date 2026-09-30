using System.Security.Claims;

namespace TaskFlow.API.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        if (
            Guid.TryParse(
                principal.FindFirstValue(ClaimTypes.NameIdentifier),
                out var userId
            )
        )
        {
            return userId;
        }

        throw new UnauthorizedAccessException("Invalid or missing user identifier");
    }
}
