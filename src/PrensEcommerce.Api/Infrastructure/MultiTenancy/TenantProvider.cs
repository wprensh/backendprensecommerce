namespace PrensEcommerce.Api.Infrastructure.MultiTenancy;

/// <summary>
/// Implementación Scoped: una instancia por petición HTTP, compartida por
/// <see cref="ITenantProvider"/> y <see cref="ITenantSetter"/>.
/// </summary>
internal sealed class TenantProvider : ITenantProvider, ITenantSetter
{
    private TenantInfo? _current;

    public TenantInfo? Current => _current;

    public bool HasTenant => _current is not null;

    public Guid TenantId => _current?.Id
        ?? throw new InvalidOperationException("No se ha resuelto ningún tenant para esta petición.");

    public void SetTenant(TenantInfo tenant)
    {
        ArgumentNullException.ThrowIfNull(tenant);

        if (_current is not null && _current.Id != tenant.Id)
        {
            throw new InvalidOperationException("El tenant ya fue establecido para esta petición.");
        }

        _current = tenant;
    }
}
