using PrensEcommerce.Api.Common.Security;
using PrensEcommerce.Api.Domain;

namespace PrensEcommerce.Api.Features.Auth;

public sealed record AuthUser(Guid Id, string Name, string Email);

/// <summary>Respuesta de login/registro, alineada con <c>User</c> del frontend.</summary>
public sealed record AuthResponse(string AccessToken, DateTimeOffset ExpiresAt, AuthUser User);

public sealed class AuthResponseFactory(ITokenService tokens)
{
    public AuthResponse Create(User user)
    {
        var (token, expiresAt) = tokens.CreateToken(user);
        return new AuthResponse(token, expiresAt, new AuthUser(user.Id, user.Name, user.Email));
    }
}
