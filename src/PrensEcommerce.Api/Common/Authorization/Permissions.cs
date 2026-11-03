namespace PrensEcommerce.Api.Common.Authorization;

public sealed record PermissionDefinition(string Code, string Group, string Description);

/// <summary>
/// Catálogo cerrado de permisos. Los roles guardan estos códigos (columna <c>identity.roles.permissions</c>)
/// y el JWT lleva los efectivos en el claim <see cref="AppPermissionClaims.Permission"/>.
/// Cada código es también el nombre de una política de autorización.
/// El comodín <see cref="All"/> (<c>"*"</c>) concede todos los permisos (rol Admin).
/// </summary>
public static class Permissions
{
    public const string Wildcard = "*";

    public const string UsersRead = "users.read";
    public const string UsersManage = "users.manage";
    public const string RolesRead = "roles.read";
    public const string RolesManage = "roles.manage";
    public const string CatalogRead = "catalog.read";
    public const string CatalogWrite = "catalog.write";
    public const string OrdersRead = "orders.read";
    public const string OrdersWrite = "orders.write";
    public const string CustomersRead = "customers.read";
    public const string SupportRead = "support.read";
    public const string SupportWrite = "support.write";
    public const string CrmRead = "crm.read";
    public const string CrmWrite = "crm.write";
    public const string AnalyticsRead = "analytics.read";

    public static IReadOnlyList<PermissionDefinition> All { get; } =
    [
        new(UsersRead, "Usuarios", "Ver usuarios"),
        new(UsersManage, "Usuarios", "Crear, editar y desactivar usuarios"),
        new(RolesRead, "Roles", "Ver roles y permisos"),
        new(RolesManage, "Roles", "Crear, editar y eliminar roles"),
        new(CatalogRead, "Catálogo", "Ver productos y catálogos en la administración"),
        new(CatalogWrite, "Catálogo", "Gestionar productos y catálogos"),
        new(OrdersRead, "Pedidos", "Ver pedidos"),
        new(OrdersWrite, "Pedidos", "Gestionar pedidos y devoluciones"),
        new(CustomersRead, "Clientes", "Ver fichas de clientes"),
        new(SupportRead, "Atención al cliente", "Ver tickets de soporte"),
        new(SupportWrite, "Atención al cliente", "Responder y gestionar tickets"),
        new(CrmRead, "CRM y marketing", "Ver CRM, fidelización y automatizaciones"),
        new(CrmWrite, "CRM y marketing", "Gestionar CRM, fidelización y automatizaciones"),
        new(AnalyticsRead, "Analítica", "Ver informes y analítica"),
    ];

    private static readonly HashSet<string> Codes = All.Select(p => p.Code).ToHashSet(StringComparer.Ordinal);

    /// <summary>Código válido para guardar en un rol (incluye el comodín).</summary>
    public static bool IsValid(string code) => code == Wildcard || Codes.Contains(code);

    /// <summary>Expande el comodín y descarta códigos desconocidos.</summary>
    public static IReadOnlyList<string> Expand(IEnumerable<string> codes)
    {
        var set = codes.ToHashSet(StringComparer.Ordinal);
        return set.Contains(Wildcard)
            ? All.Select(p => p.Code).ToList()
            : All.Select(p => p.Code).Where(set.Contains).ToList();
    }
}

public static class AppPermissionClaims
{
    /// <summary>Claim (repetible) con cada permiso efectivo del usuario.</summary>
    public const string Permission = "perm";
}
