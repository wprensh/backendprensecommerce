using FluentValidation;
using PrensEcommerce.Api.Common.Abstractions;
using PrensEcommerce.Api.Common.Validation;
using PrensEcommerce.Api.Domain;
using PrensEcommerce.Api.Infrastructure.Persistence;

namespace PrensEcommerce.Api.Features.Catalog.CreateProduct;

/// <summary>POST /api/products — alta de producto en la tienda actual (requiere autenticación).</summary>
public static class CreateProduct
{
    public sealed record Command(
        string Name, string? Description, decimal Price, int DiscountPercent,
        string Category, string? ImageUrl, int Stock);

    public sealed record Response(Guid Id);

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(c => c.Name).NotEmpty().MaximumLength(200);
            RuleFor(c => c.Description).MaximumLength(4000);
            RuleFor(c => c.Price).GreaterThan(0);
            RuleFor(c => c.DiscountPercent).InclusiveBetween(0, 100);
            RuleFor(c => c.Category).NotEmpty().MaximumLength(50);
            RuleFor(c => c.Stock).GreaterThanOrEqualTo(0);
        }
    }

    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapPost("/products", async (Command command, Handler handler, CancellationToken ct) =>
                {
                    var response = await handler.HandleAsync(command, ct);
                    return TypedResults.Created($"/api/products/{response.Id}", response);
                })
                .WithValidation<Command>()
                .RequireAuthorization()
                .WithTags("Catalog")
                .WithName("CreateProduct");
    }

    public sealed class Handler(AppDbContext db) : IHandler
    {
        public async Task<Response> HandleAsync(Command command, CancellationToken ct)
        {
            var product = new Product
            {
                Name = command.Name.Trim(),
                Description = command.Description?.Trim() ?? string.Empty,
                Price = command.Price,
                DiscountPercent = command.DiscountPercent,
                Category = command.Category.Trim().ToLowerInvariant(),
                ImageUrl = command.ImageUrl ?? string.Empty,
                Stock = command.Stock,
            };

            // TenantId lo asigna AppDbContext a partir de ITenantProvider.
            db.Products.Add(product);
            await db.SaveChangesAsync(ct);

            return new Response(product.Id);
        }
    }
}
