using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PrensEcommerce.Api.Common.Authorization;
using PrensEcommerce.Api.Common.Caching;
using PrensEcommerce.Api.Common.Endpoints;
using PrensEcommerce.Api.Common.Errors;
using PrensEcommerce.Api.Common.Security;
using PrensEcommerce.Api.Domain;
using PrensEcommerce.Api.Features.Auth;
using PrensEcommerce.Api.Features.Automations.Engine;
using PrensEcommerce.Api.Features.Cart;
using PrensEcommerce.Api.Features.Chatbot;
using PrensEcommerce.Api.Features.Checkout;
using PrensEcommerce.Api.Infrastructure.Media;
using PrensEcommerce.Api.Infrastructure.Payments;
using PrensEcommerce.Api.Features.Loyalty;
using PrensEcommerce.Api.Features.Support;
using PrensEcommerce.Api.Infrastructure.MultiTenancy;
using PrensEcommerce.Api.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

// ─────────────────────────────────────────────────────────────
// 1. PostgreSQL · Entity Framework Core 9 (Npgsql)
// ─────────────────────────────────────────────────────────────
var postgresConnection = configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("Falta ConnectionStrings:Postgres.");

// AddDbContext (no pool): el contexto depende de ITenantProvider, que es Scoped por petición.
builder.Services.AddDbContext<AppDbContext>((sp, options) =>
{
    options.UseNpgsql(postgresConnection, npgsql =>
        {
            npgsql.MigrationsHistoryTable("__ef_migrations_history", Schemas.Core);
            npgsql.EnableRetryOnFailure(maxRetryCount: 3);
        })
        .UseSnakeCaseNamingConvention()
        .AddInterceptors(sp.GetRequiredService<CustomerEventsInterceptor>()); // eventos para CRM/automatizaciones

    if (builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging().EnableDetailedErrors();
    }
});

// ─────────────────────────────────────────────────────────────
// 2. Redis · IDistributedCache + HybridCache (L1 memoria, L2 Redis)
// ─────────────────────────────────────────────────────────────
var redisConnection = configuration.GetConnectionString("Redis")
    ?? throw new InvalidOperationException("Falta ConnectionStrings:Redis.");

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConnection;
    options.InstanceName = "prens:";
});
builder.Services.AddHybridCache(); // usa automáticamente el IDistributedCache (Redis) como L2
builder.Services.AddScoped<ITenantCache, RedisTenantCache>();

// ─────────────────────────────────────────────────────────────
// 3. Multi-tenancy · ITenantProvider (Scoped) + estrategias de resolución
// ─────────────────────────────────────────────────────────────
builder.Services.AddMultiTenancy(configuration);

// ─────────────────────────────────────────────────────────────
// 4. Autenticación JWT (el token lleva el claim tenant_id)
// ─────────────────────────────────────────────────────────────
builder.Services.AddOptions<JwtOptions>()
    .Bind(configuration.GetSection(JwtOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
    {
        bearer.MapInboundClaims = false;
        bearer.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Value.Issuer,
            ValidAudience = jwt.Value.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Value.SigningKey)),
            NameClaimType = "name",
            RoleClaimType = AppClaims.Role,
        };
    });
builder.Services.AddAuthorizationBuilder().AddAppPolicies();
builder.Services.AddPermissionPolicies();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ITokenService, JwtTokenService>();
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

// ─────────────────────────────────────────────────────────────
// 5. Vertical slices · endpoints, handlers y validadores por descubrimiento
// ─────────────────────────────────────────────────────────────
builder.Services.AddFeatureSlices(typeof(Program).Assembly);
builder.Services.AddValidatorsFromAssemblyContaining<Program>(includeInternalTypes: true);
builder.Services.AddScoped<CartStore>();
builder.Services.AddCrmAndAutomations(configuration);
builder.Services.AddScoped<AuthResponseFactory>();
builder.Services.AddScoped<UserAccessLoader>();
builder.Services.AddScoped<TicketQueries>();
builder.Services.AddScoped<LoyaltyService>();
builder.Services.AddOptions<LoyaltyOptions>()
    .Bind(configuration.GetSection(LoyaltyOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Checkout y pagos: reglas de precio + pasarela enchufable (Payments:Provider).
builder.Services.AddOptions<CheckoutOptions>()
    .Bind(configuration.GetSection(CheckoutOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddPayments(configuration);

// Asistente virtual (chatbot): abierto a visitantes, con límite de mensajes por IP.
builder.Services.AddChatbotRateLimit();

// Imágenes subidas desde la administración (disco local; cambiable por S3/Blob).
builder.Services.AddMedia(configuration);

// ─────────────────────────────────────────────────────────────
// 6. Transversales · errores, OpenAPI, CORS, health checks
// ─────────────────────────────────────────────────────────────
builder.Services.AddProblemDetails();
// Enums como texto en camelCase ("card", "transfer"), igual que los tipos del frontend.
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddOpenApi();

builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
    .SetIsOriginAllowedToAllowWildcardSubdomains()
    .AllowAnyHeader()
    .AllowAnyMethod()));

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("postgres")
    .AddRedis(redisConnection, name: "redis");

var app = builder.Build();

// ─────────────────────────────────────────────────────────────
// Pipeline HTTP — el orden importa
// ─────────────────────────────────────────────────────────────
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi(); // /openapi/v1.json
}
else
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseMedia();              // imágenes públicas: antes de exigir tenant
app.UseAuthentication();     // 1º: conocer al usuario (y su claim tenant_id)
app.UseTenantResolution();   // 2º: resolver tenant y validar que coincide con el token
app.UseAuthorization();      // 3º: políticas, ya con tenant disponible
app.UseRateLimiter();        // límites por endpoint (p. ej. el chatbot)

app.MapHealthChecks("/health");
app.MapFeatureSlices("/api");

await app.InitializeDatabaseAsync();
await app.RunAsync();

/// <summary>Expuesto para WebApplicationFactory en los tests de integración.</summary>
public partial class Program;
