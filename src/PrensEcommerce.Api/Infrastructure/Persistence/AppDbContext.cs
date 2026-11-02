using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PrensEcommerce.Api.Domain;
using PrensEcommerce.Api.Infrastructure.MultiTenancy;

namespace PrensEcommerce.Api.Infrastructure.Persistence;

/// <summary>
/// DbContext único (una base de datos, un esquema) con discriminador <c>tenant_id</c>.
/// El aislamiento se garantiza en dos puntos:
/// <list type="number">
/// <item>Filtro global de consultas sobre toda entidad <see cref="ITenantEntity"/>.</item>
/// <item><see cref="SaveChangesAsync(bool, CancellationToken)"/> asigna el tenant al insertar e impide cambiarlo.</item>
/// </list>
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options, ITenantProvider tenantProvider)
    : DbContext(options)
{
    public const string Schema = "ecommerce";

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<User> Users => Set<User>();

    public DbSet<Order> Orders => Set<Order>();

    /// <summary>
    /// Leído por el filtro global en cada consulta. EF Core lo parametriza por instancia de
    /// DbContext, así que el modelo cacheado sirve para todos los tenants.
    /// Sin tenant (rutas excluidas) vale <see cref="Guid.Empty"/> y no devuelve filas.
    /// </summary>
    internal Guid CurrentTenantId => tenantProvider.Current?.Id ?? Guid.Empty;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes()
                     .Where(e => typeof(ITenantEntity).IsAssignableFrom(e.ClrType) && !e.IsOwned()))
        {
            var entity = modelBuilder.Entity(entityType.ClrType);
            entity.HasQueryFilter(BuildTenantFilter(entityType.ClrType));
            entity.HasIndex(nameof(ITenantEntity.TenantId));
            entity.HasOne(typeof(Tenant)).WithMany()
                .HasForeignKey(nameof(ITenantEntity.TenantId))
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ApplyTenantAndAuditRules();
        return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyTenantAndAuditRules();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    private void ApplyTenantAndAuditRules()
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var entry in ChangeTracker.Entries<Entity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }

        foreach (var entry in ChangeTracker.Entries<ITenantEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    // TenantId siempre viene del contexto de la petición, nunca del cliente.
                    entry.Entity.TenantId = tenantProvider.TenantId;
                    break;
                case EntityState.Modified or EntityState.Deleted
                    when entry.Entity.TenantId != tenantProvider.TenantId:
                    throw new InvalidOperationException("Intento de modificar datos de otro tenant.");
                case EntityState.Modified:
                    entry.Property(e => e.TenantId).IsModified = false;
                    break;
            }
        }
    }

    // e => e.TenantId == this.CurrentTenantId
    private LambdaExpression BuildTenantFilter(Type entityType)
    {
        var parameter = Expression.Parameter(entityType, "e");
        var tenantId = Expression.Property(parameter, nameof(ITenantEntity.TenantId));
        var current = Expression.Property(Expression.Constant(this), nameof(CurrentTenantId));
        return Expression.Lambda(Expression.Equal(tenantId, current), parameter);
    }
}
