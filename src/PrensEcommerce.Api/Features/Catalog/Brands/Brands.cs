using FluentValidation;
using Microsoft.EntityFrameworkCore;
using PrensEcommerce.Api.Common.Abstractions;
using PrensEcommerce.Api.Common.Errors;
using PrensEcommerce.Api.Common.Text;
using PrensEcommerce.Api.Common.Validation;
using PrensEcommerce.Api.Domain;
using PrensEcommerce.Api.Infrastructure.Persistence;

namespace PrensEcommerce.Api.Features.Catalog.Brands;

public sealed record BrandResponse(Guid Id, string Name, string Slug, string? LogoUrl, bool IsActive, int ProductCount);

/// <summary>Datos editables de una marca (alta y modificación).</summary>
public sealed record BrandCommand(string Name, string? Slug, string? LogoUrl, bool IsActive = true);

public sealed class BrandCommandValidator : AbstractValidator<BrandCommand>
{
    public BrandCommandValidator()
    {
        RuleFor(b => b.Name).NotEmpty().MaximumLength(100)
            .Must(n => Slug.From(n ?? string.Empty).Length > 0).WithMessage("El nombre debe contener letras o números.");
        RuleFor(b => b.Slug).MaximumLength(100)
            .Matches("^[a-z0-9]+(-[a-z0-9]+)*$").When(b => !string.IsNullOrWhiteSpace(b.Slug))
            .WithMessage("Solo minúsculas, números y guiones (p. ej. sony).");
        RuleFor(b => b.LogoUrl).MaximumLength(1000);
    }
}

/// <summary>GET /api/brands?includeInactive=</summary>
public static class GetBrands
{
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapGet("/brands", async (bool? includeInactive, System.Security.Claims.ClaimsPrincipal user, Handler handler, CancellationToken ct) =>
                    TypedResults.Ok(await handler.HandleAsync(includeInactive == true && user.IsCatalogManager(), ct)))
                .WithTags("Catalog")
                .WithName("GetBrands");
    }

    public sealed class Handler(AppDbContext db) : IHandler
    {
        public async Task<IReadOnlyList<BrandResponse>> HandleAsync(bool includeInactive, CancellationToken ct) =>
            await db.Brands.AsNoTracking()
                .Where(b => includeInactive || b.IsActive)
                .OrderBy(b => b.Name)
                .Select(b => new BrandResponse(b.Id, b.Name, b.Slug, b.LogoUrl, b.IsActive,
                    db.Products.Count(p => p.BrandId == b.Id)))
                .ToListAsync(ct);
    }
}

/// <summary>POST /api/brands</summary>
public static class CreateBrand
{
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapPost("/brands", async (BrandCommand command, Handler handler, CancellationToken ct) =>
                    (await handler.HandleAsync(command, ct)).Match(b => TypedResults.Created($"/api/brands/{b.Id}", b)))
                .WithValidation<BrandCommand>()
                .RequireCatalogManager()
                .WithTags("Catalog")
                .WithName("CreateBrand")
                .Produces<BrandResponse>(StatusCodes.Status201Created)
                .ProducesProblem(StatusCodes.Status409Conflict);
    }

    public sealed class Handler(AppDbContext db) : IHandler
    {
        public async Task<Result<BrandResponse>> HandleAsync(BrandCommand command, CancellationToken ct)
        {
            var slug = Slug.From(string.IsNullOrWhiteSpace(command.Slug) ? command.Name : command.Slug);
            if (await db.Brands.AnyAsync(b => b.Slug == slug, ct))
            {
                return Error.Conflict("Brand.DuplicateSlug", $"Ya existe una marca con el identificador «{slug}».");
            }

            var brand = new Brand
            {
                Name = command.Name.Trim(),
                Slug = slug,
                LogoUrl = string.IsNullOrWhiteSpace(command.LogoUrl) ? null : command.LogoUrl.Trim(),
                IsActive = command.IsActive,
            };
            db.Brands.Add(brand);
            await db.SaveChangesAsync(ct);

            return new BrandResponse(brand.Id, brand.Name, brand.Slug, brand.LogoUrl, brand.IsActive, 0);
        }
    }
}

/// <summary>PUT /api/brands/{id}</summary>
public static class UpdateBrand
{
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapPut("/brands/{id:guid}", async (Guid id, BrandCommand command, Handler handler, CancellationToken ct) =>
                    (await handler.HandleAsync(id, command, ct)).Match(TypedResults.Ok))
                .WithValidation<BrandCommand>()
                .RequireCatalogManager()
                .WithTags("Catalog")
                .WithName("UpdateBrand")
                .Produces<BrandResponse>()
                .ProducesProblem(StatusCodes.Status404NotFound)
                .ProducesProblem(StatusCodes.Status409Conflict);
    }

    public sealed class Handler(AppDbContext db) : IHandler
    {
        public async Task<Result<BrandResponse>> HandleAsync(Guid id, BrandCommand command, CancellationToken ct)
        {
            var brand = await db.Brands.FirstOrDefaultAsync(b => b.Id == id, ct);
            if (brand is null)
            {
                return Error.NotFound("Brand.NotFound", $"No existe la marca {id}.");
            }

            var slug = Slug.From(string.IsNullOrWhiteSpace(command.Slug) ? command.Name : command.Slug);
            if (await db.Brands.AnyAsync(b => b.Slug == slug && b.Id != id, ct))
            {
                return Error.Conflict("Brand.DuplicateSlug", $"Ya existe una marca con el identificador «{slug}».");
            }

            brand.Name = command.Name.Trim();
            brand.Slug = slug;
            brand.LogoUrl = string.IsNullOrWhiteSpace(command.LogoUrl) ? null : command.LogoUrl.Trim();
            brand.IsActive = command.IsActive;
            await db.SaveChangesAsync(ct);

            var productCount = await db.Products.CountAsync(p => p.BrandId == id, ct);
            return new BrandResponse(brand.Id, brand.Name, brand.Slug, brand.LogoUrl, brand.IsActive, productCount);
        }
    }
}

/// <summary>DELETE /api/brands/{id} — solo si no tiene productos.</summary>
public static class DeleteBrand
{
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapDelete("/brands/{id:guid}", async (Guid id, Handler handler, CancellationToken ct) =>
                    (await handler.HandleAsync(id, ct)).Match(_ => TypedResults.NoContent()))
                .RequireCatalogManager()
                .WithTags("Catalog")
                .WithName("DeleteBrand")
                .ProducesProblem(StatusCodes.Status404NotFound)
                .ProducesProblem(StatusCodes.Status409Conflict);
    }

    public sealed class Handler(AppDbContext db) : IHandler
    {
        public async Task<Result<bool>> HandleAsync(Guid id, CancellationToken ct)
        {
            var brand = await db.Brands.FirstOrDefaultAsync(b => b.Id == id, ct);
            if (brand is null)
            {
                return Error.NotFound("Brand.NotFound", $"No existe la marca {id}.");
            }

            var productCount = await db.Products.CountAsync(p => p.BrandId == id, ct);
            if (productCount > 0)
            {
                return Error.Conflict("Brand.HasProducts",
                    $"La marca tiene {productCount} producto(s). Reasígnalos o desactiva la marca.");
            }

            db.Brands.Remove(brand);
            await db.SaveChangesAsync(ct);
            return true;
        }
    }
}
