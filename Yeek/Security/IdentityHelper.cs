using System.Security.Claims;

namespace Yeek.Security;

public static class IdentityHelper
{
    public static Guid? GetUserId(this IEnumerable<Claim> claims)
    {
        var nameClaim = claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
        if (nameClaim != null && Guid.TryParse(nameClaim.Value, out var userId))
        {
            return userId;
        }

        return null;
    }
}