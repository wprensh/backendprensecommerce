using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PrensEcommerce.Api.Domain;

namespace PrensEcommerce.Api.Features.Automations.Engine;

/// <summary>
/// Registra un <see cref="CustomerEvent"/> en la MISMA transacción que el cambio de negocio
/// (alta de cliente, pedido creado, pedido pagado, ticket cerrado). Ningún slice tiene que
/// acordarse de avisar al CRM y no se pierde ningún evento si el proceso cae justo después.
/// </summary>
internal sealed class CustomerEventsInterceptor(TimeProvider clock) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Capture(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Capture(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    internal void Capture(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = clock.GetUtcNow();
        var events = new List<CustomerEvent>();

        foreach (var entry in context.ChangeTracker.Entries())
        {
            var evt = entry.Entity switch
            {
                User user when entry.State == EntityState.Added =>
                    NewEvent(user.TenantId, user.Id, AutomationTrigger.UserRegistered, null, null, now),

                Order order when entry.State == EntityState.Added =>
                    NewEvent(order.TenantId, order.UserId, AutomationTrigger.OrderPlaced, order.Id, order.Total, now),

                Order order when ChangedTo(entry, nameof(Order.Status), OrderStatus.Paid) =>
                    NewEvent(order.TenantId, order.UserId, AutomationTrigger.OrderPaid, order.Id, order.Total, now),

                SupportTicket { CustomerId: { } customerId } ticket when ChangedTo(entry, nameof(SupportTicket.Status), TicketStatus.Closed) =>
                    NewEvent(ticket.TenantId, customerId, AutomationTrigger.TicketClosed, ticket.Id, null, now),

                _ => null,
            };

            if (evt is not null)
            {
                events.Add(evt);
            }
        }

        // El DbContext ya asignó TenantId a las entidades de negocio antes de llegar aquí,
        // así que cada evento copia el tenant de la entidad que lo origina.
        context.AddRange(events);
    }

    private static bool ChangedTo<T>(EntityEntry entry, string property, T value)
        where T : struct, Enum
    {
        if (entry.State != EntityState.Modified)
        {
            return false;
        }

        var prop = entry.Property(property);
        return prop.IsModified && prop.CurrentValue is T current && current.Equals(value) &&
               !(prop.OriginalValue is T original && original.Equals(value));
    }

    private static CustomerEvent NewEvent(
        Guid tenantId, Guid userId, AutomationTrigger type, Guid? referenceId, decimal? amount, DateTimeOffset now) => new()
    {
        TenantId = tenantId,
        UserId = userId,
        Type = type,
        ReferenceId = referenceId,
        Amount = amount,
        OccurredAt = now,
        CreatedAt = now,
    };
}
