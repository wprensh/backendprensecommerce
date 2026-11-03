using Microsoft.EntityFrameworkCore;
using PrensEcommerce.Api.Infrastructure.Persistence;

namespace PrensEcommerce.Api.Common.Authorization;

public sealed record UserAccess(IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions);

/// <summary>Roles del usuario y permisos efectivos (unión de los de sus roles, con el comodín expandido).</summary>
public sealed class UserAccessLoader(AppDbContext db)
{
    public async Task<UserAccess> LoadAsync(Guid userId, CancellationToken ct)
    {
        var roles = await db.UserRoles.AsNoTracking()
            .Where(ur => ur.UserId == userId)
            .Select(ur => new { ur.Role!.Name, ur.Role.Permissions })
            .ToListAsync(ct);

        return new UserAccess(
            roles.Select(r => r.Name).Order(StringComparer.Ordinal).ToList(),
            Permissions.Expand(roles.SelectMany(r => r.Permissions)));
    }
}
