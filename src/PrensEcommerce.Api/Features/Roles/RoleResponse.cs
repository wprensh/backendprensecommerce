using System.Linq.Expressions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using PrensEcommerce.Api.Common.Authorization;
using PrensEcommerce.Api.Common.Errors;
using PrensEcommerce.Api.Domain;
using PrensEcommerce.Api.Infrastructure.Persistence;

namespace PrensEcommerce.Api.Features.Roles;

public sealed record RoleResponse(
    Guid Id,
    string Name,
    string? Description,
    bool IsSystem,
    IReadOnlyList<string> Permissions,
    int UserCount)
{
    /// <summary>Proyección traducible a SQL; el recuento de usuarios es una subconsulta correlacionada.</summary>
    public static Expression<Func<Role, RoleResponse>> Projection(AppDbContext db) => r => new RoleResponse(
        r.Id, r.Name, r.Description, r.IsSystem, r.Permissions, db.UserRoles.Count(ur => ur.RoleId == r.Id));
}

/// <summary>Cuerpo común de alta y edición de rol.</summary>
public sealed record RoleCommand(string Name, string? Description, IReadOnlyList<string> Permissions);

public sealed class RoleCommandValidator : AbstractValidator<RoleCommand>
{
    public RoleCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(100);
        RuleFor(c => c.Description).MaximumLength(500);
        RuleFor(c => c.Permissions).NotNull();
        // El comodín "*" queda reservado al rol de sistema Admin.
        RuleForEach(c => c.Permissions)
            .Must(code => code != Common.Authorization.Permissions.Wildcard && Common.Authorization.Permissions.IsValid(code))
            .WithMessage("Permiso desconocido: '{PropertyValue}'.");
    }
}

internal static class RoleRules
{
    public const string BasePath = "/admin/roles";
    public const string Tag = "Admin · Roles";

    public static readonly Error NotFound = Error.NotFound("Roles.NotFound", "El rol no existe.");

    public static async Task<bool> NameInUseAsync(AppDbContext db, string name, Guid? exceptId, CancellationToken ct)
    {
        // ILIKE sin comodines = comparación insensible a mayúsculas en PostgreSQL.
        var pattern = name.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");
        return await db.Roles.AnyAsync(r => EF.Functions.ILike(r.Name, pattern) && r.Id != exceptId, ct);
    }

    public static List<string> Normalize(IEnumerable<string> permissions) =>
        permissions.Distinct().Order(StringComparer.Ordinal).ToList();

    public static Task<RoleResponse?> FindAsync(AppDbContext db, Guid id, CancellationToken ct) =>
        db.Roles.AsNoTracking().Where(r => r.Id == id).Select(RoleResponse.Projection(db)).FirstOrDefaultAsync(ct);
}
