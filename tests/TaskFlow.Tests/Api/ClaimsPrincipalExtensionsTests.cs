using System.Security.Claims;
using System.Security.Principal;
using TaskFlow.API.Extensions;

namespace TaskFlow.Tests.Api;

public class ClaimsPrincipalExtensionsTests
{
    private static ClaimsPrincipal WithClaim(string type, string value) =>
        new(new ClaimsIdentity([new Claim(type, value)]));

    [Fact]
    public void GetUserId_NameIdentifierLaGuid_TraDung()
    {
        var id = Guid.NewGuid();
        var principal = WithClaim(ClaimTypes.NameIdentifier, id.ToString());

        Assert.Equal(id, principal.GetUserId());
    }

    [Fact]
    public void GetUserId_NameIdentifierKhongPhaiGuid_NemUnauthorized()
    {
        var principal = WithClaim(ClaimTypes.NameIdentifier, "khong-phai-guid");

        Assert.Throws<UnauthorizedAccessException>(() => principal.GetUserId());
    }

    [Fact]
    public void GetUserId_KhongCoClaim_NemUnauthorized()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        Assert.Throws<UnauthorizedAccessException>(() => principal.GetUserId());
    }
}
