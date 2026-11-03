using FluentValidation;
using PrensEcommerce.Api.Common.Abstractions;
using PrensEcommerce.Api.Common.Errors;
using PrensEcommerce.Api.Common.Validation;
using PrensEcommerce.Api.Domain;
using PrensEcommerce.Api.Infrastructure.Persistence;

namespace PrensEcommerce.Api.Features.Catalog.CreateProduct;

/// <summary>POST /api/products — alta de producto en la tienda actual (requiere autenticación).</summary>
public static class CreateProduct
{
    public sealed record Command(
        string Name, string? Sku, string? Description, decimal Price, int DiscountPercent,
        Guid CategoryId, Guid? BrandId, string? ImageUrl, int Stock, bool IsActive = true) : IProductData
    {
        public IReadOnlyList<string>? Images { get; init; }
    }

    public sealed record Response(Guid Id);

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator() => Include(new ProductDataValidator<Command>());
    }

    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapPost("/products", async (Command command, Handler handler, CancellationToken ct) =>
                    (await handler.HandleAsync(command, ct))
                        .Match(r => TypedResults.Created($"/api/products/{r.Id}", r)))
                .WithValidation<Command>()
                .RequireCatalogManager()
                .WithTags("Catalog")
                .WithName("CreateProduct")
                .Produces<Response>(StatusCodes.Status201Created)
                .ProducesProblem(StatusCodes.Status409Conflict);
    }

    public sealed class Handler(AppDbContext db) : IHandler
    {
        public async Task<Result<Response>> HandleAsync(Command command, CancellationToken ct)
        {
            if (await ProductRules.CheckReferencesAsync(db, command, null, ct) is { } error)
            {
                return error;
            }

            var (images, mainImage) = ProductRules.NormalizeImages(command);
            var product = new Product
            {
                Name = command.Name.Trim(),
                Sku = ProductRules.NormalizeSku(command.Sku),
                Description = command.Description?.Trim() ?? string.Empty,
                Price = command.Price,
                DiscountPercent = command.DiscountPercent,
                CategoryId = command.CategoryId,
                BrandId = command.BrandId,
                ImageUrl = mainImage,
                Images = images,
                Stock = command.Stock,
                IsActive = command.IsActive,
            };

            // TenantId lo asigna AppDbContext a partir de ITenantProvider.
            db.Products.Add(product);
            await db.SaveChangesAsync(ct);

            return new Response(product.Id);
        }
    }
}
