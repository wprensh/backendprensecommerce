namespace PrensEcommerce.Api.Domain;

// ── Esquema "identity": usuarios, roles y su relación ─────────────────

/// <summary>Usuario de una tienda (cliente o personal). El email es único por tenant, no global.</summary>
public sealed class User : TenantEntity
{
    public required string Name { get; set; }

    public required string Email { get; set; }

    public string PasswordHash { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Consentimiento para comunicaciones de marketing (RGPD).</summary>
    public bool AcceptsMarketing { get; set; }

    /// <summary>Etiquetas libres para segmentación CRM (ej. "vip", "mayorista").</summary>
    public List<string> Tags { get; set; } = [];

    public DateTimeOffset? LastLoginAt { get; set; }

    public List<UserRole> Roles { get; init; } = [];
}

/// <summary>Rol definido por cada tienda, con sus permisos.</summary>
public sealed class Role : TenantEntity
{
    public required string Name { get; set; }

    public string? Description { get; set; }

    /// <summary>Permisos del rol (ej. "catalog.write", "users.manage"). Columna text[] en PostgreSQL.</summary>
    public List<string> Permissions { get; set; } = [];

    /// <summary>Los roles de sistema no se pueden borrar desde la administración.</summary>
    public bool IsSystem { get; set; }
}

public sealed class UserRole : TenantEntity
{
    public Guid UserId { get; set; }

    public Guid RoleId { get; set; }

    public Role? Role { get; set; }
}

/// <summary>Roles de sistema que se crean con cada tienda.</summary>
public static class SystemRoles
{
    public const string Admin = "Admin";
    public const string CatalogManager = "CatalogManager";
    public const string CustomerService = "CustomerService";
    public const string Marketing = "Marketing";
    public const string Customer = "Customer";
}
