using Microsoft.EntityFrameworkCore;
using PrensEcommerce.Api.Common.Security;
using PrensEcommerce.Api.Domain;
using PrensEcommerce.Api.Infrastructure.Persistence;

namespace PrensEcommerce.Api.Features.Auth;

public sealed record AuthUser(Guid Id, string Name, string Email, IReadOnlyList<string> Roles);

/// <summary>Respuesta de login/registro, alineada con <c>User</c> del frontend.</summary>
public sealed record AuthResponse(string AccessToken, DateTimeOffset ExpiresAt, AuthUser User);

/// <summary>Carga los roles del usuario y emite su token. Compartido por Login y Register.</summary>
public sealed class AuthResponseFactory(AppDbContext db, ITokenService tokens)
{
    public async Task<AuthResponse> CreateAsync(User user, CancellationToken ct)
    {
        var roles = await db.UserRoles
            .Where(ur => ur.UserId == user.Id)
            .Select(ur => ur.Role!.Name)
            .ToListAsync(ct);

        var (token, expiresAt) = tokens.CreateToken(user, roles);
        return new AuthResponse(token, expiresAt, new AuthUser(user.Id, user.Name, user.Email, roles));
    }
}
