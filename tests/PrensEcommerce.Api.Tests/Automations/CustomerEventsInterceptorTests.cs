using Microsoft.EntityFrameworkCore;
using PrensEcommerce.Api.Domain;
using PrensEcommerce.Api.Features.Automations.Engine;
using PrensEcommerce.Api.Infrastructure.MultiTenancy;
using PrensEcommerce.Api.Infrastructure.Persistence;

namespace PrensEcommerce.Api.Tests.Automations;

/// <summary>Solo usa el change tracker: no necesita conexión a PostgreSQL.</summary>
public sealed class CustomerEventsInterceptorTests : IDisposable
{
    private static readonly TenantInfo Tenant = new(Guid.NewGuid(), "acme", "Acme");
    private readonly AppDbContext _db;
    private readonly CustomerEventsInterceptor _interceptor = new(TimeProvider.System);

    public CustomerEventsInterceptorTests()
    {
        var tenant = new TenantProvider();
        tenant.SetTenant(Tenant);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=unused")
            .UseSnakeCaseNamingConvention()
            .Options;
        _db = new AppDbContext(options, tenant);
    }

    public void Dispose() => _db.Dispose();

    private List<CustomerEvent> CapturedEvents()
    {
        _interceptor.Capture(_db);
        return [.. _db.ChangeTracker.Entries<CustomerEvent>().Select(e => e.Entity)];
    }

    private static Order NewOrder(Guid userId, OrderStatus status) => new()
    {
        TenantId = Tenant.Id,
        UserId = userId,
        OrderNumber = "PE-000001",
        Status = status,
        Total = 50m,
        ShippingAddress = new ShippingAddress("Ana", "C/ 1", "Madrid", "600"),
    };

    [Fact]
    public void New_user_and_new_order_produce_registration_and_order_events()
    {
        var user = new User { TenantId = Tenant.Id, Name = "Ana", Email = "ana@test.com" };
        _db.Users.Add(user);
        var order = NewOrder(user.Id, OrderStatus.Pending);
        _db.Orders.Add(order);

        var events = CapturedEvents();

        Assert.Contains(events, e => e.Type == AutomationTrigger.UserRegistered && e.UserId == user.Id && e.TenantId == Tenant.Id);
        Assert.Contains(events, e => e.Type == AutomationTrigger.OrderPlaced && e.ReferenceId == order.Id && e.Amount == 50m);
    }

    [Fact]
    public void Order_changing_to_paid_produces_one_order_paid_event()
    {
        var order = NewOrder(Guid.NewGuid(), OrderStatus.Pending);
        _db.Orders.Attach(order);
        order.Status = OrderStatus.Paid;

        var events = CapturedEvents();

        var evt = Assert.Single(events);
        Assert.Equal(AutomationTrigger.OrderPaid, evt.Type);
        Assert.Equal(order.Id, evt.ReferenceId);
    }

    [Fact]
    public void Other_order_changes_produce_no_events()
    {
        var order = NewOrder(Guid.NewGuid(), OrderStatus.Paid);
        _db.Orders.Attach(order);
        order.Status = OrderStatus.Shipped;

        Assert.Empty(CapturedEvents());
    }

    [Fact]
    public void Order_created_already_paid_produces_placed_and_paid_events()
    {
        var order = NewOrder(Guid.NewGuid(), OrderStatus.Paid);
        _db.Orders.Add(order);

        var types = CapturedEvents().Select(e => e.Type).Order().ToList();

        Assert.Equal([AutomationTrigger.OrderPlaced, AutomationTrigger.OrderPaid], types);
    }
}
