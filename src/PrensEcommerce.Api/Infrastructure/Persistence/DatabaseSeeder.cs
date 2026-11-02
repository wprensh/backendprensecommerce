using Microsoft.EntityFrameworkCore;
using PrensEcommerce.Api.Domain;
using PrensEcommerce.Api.Infrastructure.MultiTenancy;

namespace PrensEcommerce.Api.Infrastructure.Persistence;

/// <summary>Aplica migraciones y, opcionalmente, crea la tienda "demo" con productos de ejemplo.</summary>
internal static class DatabaseSeeder
{
    public const string DemoTenantSlug = "demo";

    public static async Task InitializeDatabaseAsync(this WebApplication app)
    {
        var config = app.Configuration.GetSection("Database");
        if (!config.GetValue<bool>("ApplyMigrationsOnStartup"))
        {
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        if (!config.GetValue<bool>("SeedDemoData") ||
            await db.Tenants.AnyAsync(t => t.Slug == DemoTenantSlug))
        {
            return;
        }

        var tenant = new Tenant { Slug = DemoTenantSlug, Name = "Prens Demo" };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        // Fijamos el tenant en el scope para que el DbContext asigne TenantId a los productos.
        scope.ServiceProvider.GetRequiredService<ITenantSetter>()
            .SetTenant(new TenantInfo(tenant.Id, tenant.Slug, tenant.Name));

        db.Products.AddRange(
            new Product { Name = "Auriculares inalámbricos", Category = "audio", Price = 89.90m, DiscountPercent = 15, Rating = 4.6m, ReviewCount = 128, Stock = 40 },
            new Product { Name = "Altavoz Bluetooth", Category = "audio", Price = 59.00m, Rating = 4.3m, ReviewCount = 76, Stock = 25 },
            new Product { Name = "Lámpara de mesa", Category = "hogar", Price = 34.50m, DiscountPercent = 10, Rating = 4.1m, ReviewCount = 41, Stock = 60 },
            new Product { Name = "Zapatillas running", Category = "deportes", Price = 99.99m, DiscountPercent = 20, Rating = 4.7m, ReviewCount = 210, Stock = 18 },
            new Product { Name = "Chaqueta impermeable", Category = "moda", Price = 120.00m, Rating = 4.4m, ReviewCount = 55, Stock = 12 });

        await db.SaveChangesAsync();
    }
}
