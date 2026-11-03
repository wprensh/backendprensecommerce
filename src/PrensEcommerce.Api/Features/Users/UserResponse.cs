using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PrensEcommerce.Api.Common.Errors;
using PrensEcommerce.Api.Domain;
using PrensEcommerce.Api.Infrastructure.Persistence;

namespace PrensEcommerce.Api.Features.Users;

public sealed record UserRoleSummary(Guid Id, string Name);

public sealed record UserResponse(
    Guid Id,
    string Name,
    string Email,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt,
    IReadOnlyList<UserRoleSummary> Roles)
{
    /// <summary>Proyección traducible a SQL (sin cargar entidades completas).</summary>
    public static readonly Expression<Func<User, UserResponse>> Projection = u => new UserResponse(
        u.Id, u.Name, u.Email, u.IsActive, u.CreatedAt, u.LastLoginAt,
        u.Roles.OrderBy(ur => ur.Role!.Name).Select(ur => new UserRoleSummary(ur.RoleId, ur.Role!.Name)).ToList());
}

internal static class UserRules
{
    public const string BasePath = "/admin/users";
    public const string Tag = "Admin · Usuarios";

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    /// <summary>Comprueba que todos los roles existen en la tienda actual (el filtro global descarta los de otras).</summary>
    public static async Task<Error?> ValidateRolesAsync(AppDbContext db, IReadOnlyCollection<Guid> roleIds, CancellationToken ct)
    {
        var distinct = roleIds.Distinct().ToList();
        var found = await db.Roles.CountAsync(r => distinct.Contains(r.Id), ct);
        return found == distinct.Count
            ? null
            : Error.Validation("Users.RoleNotFound", "Alguno de los roles indicados no existe en esta tienda.");
    }

    public static Task<UserResponse?> FindAsync(AppDbContext db, Guid id, CancellationToken ct) =>
        db.Users.AsNoTracking().Where(u => u.Id == id).Select(UserResponse.Projection).FirstOrDefaultAsync(ct);

    public static readonly Error NotFound = Error.NotFound("Users.NotFound", "El usuario no existe.");
}
