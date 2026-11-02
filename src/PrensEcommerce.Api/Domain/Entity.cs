namespace PrensEcommerce.Api.Domain;

public abstract class Entity
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }
}

/// <summary>
/// Marca una entidad como perteneciente a un tenant. El DbContext:
/// (1) rellena <see cref="TenantId"/> al insertar y (2) aplica un filtro global
/// <c>WHERE tenant_id = @tenant</c> en todas las consultas.
/// </summary>
public interface ITenantEntity
{
    Guid TenantId { get; set; }
}

public abstract class TenantEntity : Entity, ITenantEntity
{
    public Guid TenantId { get; set; }
}
