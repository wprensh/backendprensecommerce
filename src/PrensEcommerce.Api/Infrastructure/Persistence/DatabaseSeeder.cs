using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PrensEcommerce.Api.Domain;
using PrensEcommerce.Api.Infrastructure.MultiTenancy;

namespace PrensEcommerce.Api.Infrastructure.Persistence;

/// <summary>
/// Aplica migraciones y, opcionalmente, crea la tienda "demo" con roles, categorías,
/// productos y un administrador de ejemplo. Solo para desarrollo.
/// </summary>
internal static class DatabaseSeeder
{
    public const string DemoTenantSlug = "demo";
    public const string DemoAdminEmail = "admin@prens.shop";
    public const string DemoAdminPassword = "Admin123!";

    /// <summary>Permisos por rol de sistema. La administración de roles podrá editarlos.</summary>
    public static readonly IReadOnlyDictionary<string, string[]> SystemRolePermissions = new Dictionary<string, string[]>
    {
        [SystemRoles.Admin] = ["*"],
        [SystemRoles.CatalogManager] = ["catalog.read", "catalog.write"],
        [SystemRoles.CustomerService] = ["support.read", "support.write", "customers.read", "orders.read"],
        [SystemRoles.Marketing] = ["crm.read", "crm.write", "customers.read"],
        [SystemRoles.Customer] = [],
    };

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

        var tenant = new Tenant { Slug = DemoTenantSlug, Name = "Prens Tecnología SHOP" };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        // Fijamos el tenant en el scope para que el DbContext asigne TenantId a todo lo demás.
        scope.ServiceProvider.GetRequiredService<ITenantSetter>()
            .SetTenant(new TenantInfo(tenant.Id, tenant.Slug, tenant.Name));

        var roles = SystemRolePermissions
            .Select(kv => new Role { Name = kv.Key, Permissions = [.. kv.Value], IsSystem = true })
            .ToDictionary(r => r.Name);
        db.Roles.AddRange(roles.Values);

        var admin = new User { Name = "Administrador", Email = DemoAdminEmail };
        admin.PasswordHash = new PasswordHasher<User>().HashPassword(admin, DemoAdminPassword);
        admin.Roles.Add(new UserRole { RoleId = roles[SystemRoles.Admin].Id });
        db.Users.Add(admin);

        var categories = new[]
        {
            new Category { Name = "Audio", Slug = "audio", SortOrder = 1 },
            new Category { Name = "Hogar", Slug = "hogar", SortOrder = 2 },
            new Category { Name = "Moda", Slug = "moda", SortOrder = 3 },
            new Category { Name = "Deportes", Slug = "deportes", SortOrder = 4 },
        }.ToDictionary(c => c.Slug);
        db.Categories.AddRange(categories.Values);

        db.Products.AddRange(
            new Product { Sku = "AUD-001", Name = "Auriculares inalámbricos", CategoryId = categories["audio"].Id, Price = 89.90m, DiscountPercent = 15, Rating = 4.6m, ReviewCount = 128, Stock = 40 },
            new Product { Sku = "AUD-002", Name = "Altavoz Bluetooth", CategoryId = categories["audio"].Id, Price = 59.00m, Rating = 4.3m, ReviewCount = 76, Stock = 25 },
            new Product { Sku = "HOG-001", Name = "Lámpara de mesa", CategoryId = categories["hogar"].Id, Price = 34.50m, DiscountPercent = 10, Rating = 4.1m, ReviewCount = 41, Stock = 60 },
            new Product { Sku = "DEP-001", Name = "Zapatillas running", CategoryId = categories["deportes"].Id, Price = 99.99m, DiscountPercent = 20, Rating = 4.7m, ReviewCount = 210, Stock = 18 },
            new Product { Sku = "MOD-001", Name = "Chaqueta impermeable", CategoryId = categories["moda"].Id, Price = 120.00m, Rating = 4.4m, ReviewCount = 55, Stock = 12 });

        db.AutomationRules.AddRange(
            new AutomationRule { Name = "Bienvenida", Trigger = AutomationTrigger.UserRegistered, Action = AutomationAction.SendEmail, Configuration = """{"template":"welcome"}""" },
            new AutomationRule { Name = "Puntos por compra", Trigger = AutomationTrigger.OrderPaid, Action = AutomationAction.AwardPoints, Configuration = """{"pointsPerEuro":1}""" },
            new AutomationRule { Name = "Carrito abandonado", Trigger = AutomationTrigger.CartAbandoned, Action = AutomationAction.SendEmail, DelayMinutes = 60, Configuration = """{"template":"cart-reminder"}""", IsActive = false });

        await db.SaveChangesAsync();
    }
}
