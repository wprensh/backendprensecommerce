namespace PrensEcommerce.Api.Infrastructure.MultiTenancy;

/// <summary>
/// Contrato de solo lectura con el tenant de la petición actual.
/// Lo consumen los slices, el DbContext y la caché (ISP: no pueden modificarlo).
/// </summary>
public interface ITenantProvider
{
    /// <summary>Tenant resuelto, o <c>null</c> si la ruta no requiere tenant (health, OpenAPI).</summary>
    TenantInfo? Current { get; }

    /// <summary>Id del tenant actual. Lanza si no hay tenant resuelto.</summary>
    Guid TenantId { get; }

    bool HasTenant { get; }
}

/// <summary>
/// Contrato de escritura: solo el middleware de resolución fija el tenant, una vez por petición.
/// </summary>
public interface ITenantSetter
{
    void SetTenant(TenantInfo tenant);
}
