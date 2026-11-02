namespace PrensEcommerce.Api.Infrastructure.MultiTenancy;

/// <summary>Busca un tenant activo por slug o Guid (DIP: el middleware no conoce EF ni Redis).</summary>
public interface ITenantStore
{
    Task<TenantInfo?> FindAsync(string identifier, CancellationToken cancellationToken);
}
