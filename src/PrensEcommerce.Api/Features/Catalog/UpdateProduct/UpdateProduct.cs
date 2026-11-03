using FluentValidation;
using Microsoft.EntityFrameworkCore;
using PrensEcommerce.Api.Common.Abstractions;
using PrensEcommerce.Api.Common.Errors;
using PrensEcommerce.Api.Common.Validation;
using PrensEcommerce.Api.Infrastructure.Persistence;

namespace PrensEcommerce.Api.Features.Catalog.UpdateProduct;

/// <summary>PUT /api/products/{id} — modifica todos los datos editables del producto.</summary>
public static class UpdateProduct
{
    public sealed record Command(
        string Name, string? Sku, string? Description, decimal Price, int DiscountPercent,
        Guid CategoryId, Guid? BrandId, string? ImageUrl, int Stock, bool IsActive) : IProductData
    {
        public IReadOnlyList<string>? Images { get; init; }
    }

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator() => Include(new ProductDataValidator<Command>());
    }

    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapPut("/products/{id:guid}", async (Guid id, Command command, Handler handler, CancellationToken ct) =>
                    (await handler.HandleAsync(id, command, ct)).Match(TypedResults.Ok))
                .WithValidation<Command>()
                .RequireCatalogManager()
                .WithTags("Catalog")
                .WithName("UpdateProduct")
                .Produces<ProductResponse>()
                .ProducesProblem(StatusCodes.Status404NotFound)
                .ProducesProblem(StatusCodes.Status409Conflict);
    }

    public sealed class Handler(AppDbContext db) : IHandler
    {
        public async Task<Result<ProductResponse>> HandleAsync(Guid id, Command command, CancellationToken ct)
        {
            var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
            if (product is null)
            {
                return Error.NotFound("Product.NotFound", $"No existe el producto {id}.");
            }

            if (await ProductRules.CheckReferencesAsync(db, command, id, ct) is { } error)
            {
                return error;
            }

            product.Name = command.Name.Trim();
            product.Sku = ProductRules.NormalizeSku(command.Sku);
            product.Description = command.Description?.Trim() ?? string.Empty;
            product.Price = command.Price;
            product.DiscountPercent = command.DiscountPercent;
            product.CategoryId = command.CategoryId;
            product.BrandId = command.BrandId;
            (product.Images, product.ImageUrl) = ProductRules.NormalizeImages(command);
            product.Stock = command.Stock;
            product.IsActive = command.IsActive;
            await db.SaveChangesAsync(ct);

            return await db.Products.AsNoTracking()
                .Where(p => p.Id == id)
                .Select(ProductResponse.Projection)
                .FirstAsync(ct);
        }
    }
}
