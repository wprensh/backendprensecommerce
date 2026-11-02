using FluentValidation;
using Microsoft.EntityFrameworkCore;
using PrensEcommerce.Api.Common.Abstractions;
using PrensEcommerce.Api.Common.Errors;
using PrensEcommerce.Api.Common.Validation;
using PrensEcommerce.Api.Domain;
using PrensEcommerce.Api.Infrastructure.Persistence;

namespace PrensEcommerce.Api.Features.Catalog.CreateProduct;

/// <summary>POST /api/products — alta de producto en la tienda actual (requiere rol Admin o CatalogManager).</summary>
public static class CreateProduct
{
    public sealed record Command(
        string Name, string? Sku, string? Description, decimal Price, int DiscountPercent,
        Guid CategoryId, string? ImageUrl, int Stock);

    public sealed record Response(Guid Id);

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(c => c.Name).NotEmpty().MaximumLength(200);
            RuleFor(c => c.Description).MaximumLength(4000);
            RuleFor(c => c.Price).GreaterThan(0);
            RuleFor(c => c.DiscountPercent).InclusiveBetween(0, 100);
            RuleFor(c => c.Sku).MaximumLength(64);
            RuleFor(c => c.CategoryId).NotEmpty();
            RuleFor(c => c.Stock).GreaterThanOrEqualTo(0);
        }
    }

    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapPost("/products", async (Command command, Handler handler, CancellationToken ct) =>
                    (await handler.HandleAsync(command, ct))
                        .Match(r => TypedResults.Created($"/api/products/{r.Id}", r)))
                .WithValidation<Command>()
                .RequireAuthorization(policy => policy.RequireRole(SystemRoles.Admin, SystemRoles.CatalogManager))
                .WithTags("Catalog")
                .WithName("CreateProduct");
    }

    public sealed class Handler(AppDbContext db) : IHandler
    {
        public async Task<Result<Response>> HandleAsync(Command command, CancellationToken ct)
        {
            if (!await db.Categories.AnyAsync(c => c.Id == command.CategoryId, ct))
            {
                return Error.Validation("Product.CategoryNotFound", "La categoría no existe en esta tienda.");
            }

            var sku = string.IsNullOrWhiteSpace(command.Sku) ? null : command.Sku.Trim().ToUpperInvariant();
            if (sku is not null && await db.Products.AnyAsync(p => p.Sku == sku, ct))
            {
                return Error.Conflict("Product.SkuInUse", $"Ya existe un producto con el SKU {sku}.");
            }

            var product = new Product
            {
                Sku = sku,
                Name = command.Name.Trim(),
                Description = command.Description?.Trim() ?? string.Empty,
                Price = command.Price,
                DiscountPercent = command.DiscountPercent,
                CategoryId = command.CategoryId,
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
