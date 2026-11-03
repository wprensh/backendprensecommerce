using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PrensEcommerce.Api.Common.Authorization;
using PrensEcommerce.Api.Domain;

namespace PrensEcommerce.Api.Common.Security;

public interface ITokenService
{
    (string Token, DateTimeOffset ExpiresAt) CreateToken(User user, UserAccess access);
}

internal sealed class JwtTokenService(IOptions<JwtOptions> options, TimeProvider clock) : ITokenService
{
    public (string Token, DateTimeOffset ExpiresAt) CreateToken(User user, UserAccess access)
    {
        var jwt = options.Value;
        var expiresAt = clock.GetUtcNow().AddMinutes(jwt.ExpirationMinutes);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Name, user.Name),
            new Claim(AppClaims.TenantId, user.TenantId.ToString()),
        };
        claims.AddRange(access.Roles.Select(role => new Claim(AppClaims.Role, role)));
        claims.AddRange(access.Permissions.Select(permission => new Claim(AppPermissionClaims.Permission, permission)));

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)), SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(jwt.Issuer, jwt.Audience, claims,
            expires: expiresAt.UtcDateTime, signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
