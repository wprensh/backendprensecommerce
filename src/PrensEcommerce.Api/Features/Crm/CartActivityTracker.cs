using Microsoft.EntityFrameworkCore;
using PrensEcommerce.Api.Infrastructure.MultiTenancy;
using PrensEcommerce.Api.Infrastructure.Persistence;

namespace PrensEcommerce.Api.Features.Crm;

/// <summary>
/// Anota cuándo cambió el carrito (que vive en Redis) para poder detectar carritos abandonados.
/// Usa un UPSERT directo para no arrastrar cambios pendientes del DbContext de la petición,
/// y nunca rompe la operación del carrito si falla.
/// </summary>
public sealed class CartActivityTracker(
    AppDbContext db,
    ITenantProvider tenant,
    TimeProvider clock,
    ILogger<CartActivityTracker> logger)
{
    public async Task TrackAsync(Guid userId, int itemCount, CancellationToken ct)
    {
        try
        {
            var now = clock.GetUtcNow();
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO crm.cart_activities (id, tenant_id, user_id, item_count, last_activity_at, created_at)
                VALUES ({Guid.CreateVersion7()}, {tenant.TenantId}, {userId}, {itemCount}, {now}, {now})
                ON CONFLICT (tenant_id, user_id) DO UPDATE SET
                    item_count = EXCLUDED.item_count,
                    last_activity_at = EXCLUDED.last_activity_at,
                    updated_at = EXCLUDED.created_at
                """, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "No se pudo registrar la actividad del carrito del usuario {UserId}", userId);
        }
    }
}
