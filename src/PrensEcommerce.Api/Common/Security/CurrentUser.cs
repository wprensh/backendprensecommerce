using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace PrensEcommerce.Api.Common.Security;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? user.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw new InvalidOperationException("El token no contiene el identificador de usuario.");
}
