using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PrensEcommerce.Api.Infrastructure.MultiTenancy;
using PrensEcommerce.Api.Infrastructure.Persistence;

namespace PrensEcommerce.Api.Features.Automations.Engine;

/// <summary>
/// Proceso en segundo plano: cada <see cref="AutomationOptions.IntervalSeconds"/> recorre las tiendas
/// activas y ejecuta el <see cref="AutomationEngine"/> en un scope propio con el tenant fijado.
/// Un fallo en una tienda no afecta al resto.
/// </summary>
internal sealed class AutomationWorker(
    IServiceScopeFactory scopes,
    IOptions<AutomationOptions> options,
    ILogger<AutomationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("Automatizaciones desactivadas (Automations:Enabled = false).");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.IntervalSeconds));
        do
        {
            try
            {
                await RunAllTenantsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Error al listar las tiendas para automatizaciones.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunAllTenantsAsync(CancellationToken ct)
    {
        List<TenantInfo> tenants;
        await using (var scope = scopes.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            tenants = await db.Tenants.AsNoTracking().Where(t => t.IsActive)
                .Select(t => new TenantInfo(t.Id, t.Slug, t.Name)).ToListAsync(ct);
        }

        foreach (var tenant in tenants)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                scope.ServiceProvider.GetRequiredService<ITenantSetter>().SetTenant(tenant);
                var result = await scope.ServiceProvider.GetRequiredService<AutomationEngine>().RunAsync(ct);

                if (result.RunsScheduled + result.Succeeded + result.Skipped + result.Failed > 0)
                {
                    logger.LogInformation("Automatizaciones {Tenant}: {@Result}", tenant.Slug, result);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Error en automatizaciones de la tienda {Tenant}", tenant.Slug);
            }
        }
    }
}
