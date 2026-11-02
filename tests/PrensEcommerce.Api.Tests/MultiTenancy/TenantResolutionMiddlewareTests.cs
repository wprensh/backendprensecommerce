using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PrensEcommerce.Api.Common.Security;
using PrensEcommerce.Api.Infrastructure.MultiTenancy;

namespace PrensEcommerce.Api.Tests.MultiTenancy;

public class TenantResolutionMiddlewareTests
{
    private static readonly TenantInfo Acme = new(Guid.NewGuid(), "acme", "Acme");

    private sealed class FakeStore : ITenantStore
    {
        public Task<TenantInfo?> FindAsync(string identifier, CancellationToken cancellationToken) =>
            Task.FromResult(identifier == "acme" ? Acme : null);
    }

    private static async Task<(int Status, bool NextCalled, TenantProvider Provider)> RunAsync(
        string? header, string path = "/api/products", string? tokenTenant = null)
    {
        var options = new MultiTenancyOptions();
        options.ExcludedPaths.Add("/health");
        var wrapped = Options.Create(options);

        var context = new DefaultHttpContext();
        context.RequestServices = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        context.Request.Path = path;
        context.Request.Host = new HostString("localhost");
        context.Response.Body = new MemoryStream();
        if (header is not null)
        {
            context.Request.Headers["X-Tenant-ID"] = header;
        }

        if (tokenTenant is not null)
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(AppClaims.TenantId, tokenTenant)], "test"));
        }

        var nextCalled = false;
        var middleware = new TenantResolutionMiddleware(
            _ => { nextCalled = true; return Task.CompletedTask; },
            wrapped,
            NullLogger<TenantResolutionMiddleware>.Instance);

        var provider = new TenantProvider();
        await middleware.InvokeAsync(context, [new HeaderTenantResolver(wrapped)], new FakeStore(), provider);

        return (context.Response.StatusCode, nextCalled, provider);
    }

    [Fact]
    public async Task Resolves_known_tenant_and_continues()
    {
        var (status, next, provider) = await RunAsync("acme");

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.True(next);
        Assert.Equal(Acme.Id, provider.TenantId);
    }

    [Fact]
    public async Task Missing_tenant_returns_400()
    {
        var (status, next, _) = await RunAsync(header: null);

        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.False(next);
    }

    [Fact]
    public async Task Unknown_tenant_returns_404()
    {
        var (status, next, _) = await RunAsync("nope");

        Assert.Equal(StatusCodes.Status404NotFound, status);
        Assert.False(next);
    }

    [Fact]
    public async Task Token_from_another_tenant_returns_403()
    {
        var (status, next, _) = await RunAsync("acme", tokenTenant: Guid.NewGuid().ToString());

        Assert.Equal(StatusCodes.Status403Forbidden, status);
        Assert.False(next);
    }

    [Fact]
    public async Task Excluded_path_skips_resolution()
    {
        var (status, next, provider) = await RunAsync(header: null, path: "/health");

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.True(next);
        Assert.False(provider.HasTenant);
    }
}
