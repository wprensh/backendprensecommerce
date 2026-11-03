using PrensEcommerce.Api.Common.Authorization;
using PrensEcommerce.Api.Common.Security;
using PrensEcommerce.Api.Domain;

namespace PrensEcommerce.Api.Features.Auth;

/// <summary>Usuario autenticado. Los permisos permiten al frontend mostrar u ocultar la administración.</summary>
public sealed record AuthUser(Guid Id, string Name, string Email, IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions);

/// <summary>Respuesta de login/registro, alineada con <c>User</c> del frontend.</summary>
public sealed record AuthResponse(string AccessToken, DateTimeOffset ExpiresAt, AuthUser User);

/// <summary>Carga roles y permisos del usuario y emite su token. Compartido por Login y Register.</summary>
public sealed class AuthResponseFactory(UserAccessLoader accessLoader, ITokenService tokens)
{
    public async Task<AuthResponse> CreateAsync(User user, CancellationToken ct)
    {
        var access = await accessLoader.LoadAsync(user.Id, ct);
        var (token, expiresAt) = tokens.CreateToken(user, access);
        return new AuthResponse(token, expiresAt,
            new AuthUser(user.Id, user.Name, user.Email, access.Roles, access.Permissions));
    }
}
