using System.Linq.Expressions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using PrensEcommerce.Api.Common.Abstractions;
using PrensEcommerce.Api.Common.Errors;
using PrensEcommerce.Api.Common.Text;
using PrensEcommerce.Api.Common.Validation;
using PrensEcommerce.Api.Domain;
using PrensEcommerce.Api.Infrastructure.Persistence;

namespace PrensEcommerce.Api.Features.Catalog.Categories;

public sealed record CategoryResponse(
    Guid Id, string Name, string Slug, string? Description, string? ImageUrl,
    Guid? ParentId, int SortOrder, bool IsActive, int ProductCount);

/// <summary>Datos editables de una categoría (alta y modificación). Sin slug se genera a partir del nombre.</summary>
public sealed record CategoryCommand(
    string Name, string? Slug, string? Description, string? ImageUrl, Guid? ParentId,
    int SortOrder = 0, bool IsActive = true);

public sealed class CategoryCommandValidator : AbstractValidator<CategoryCommand>
{
    public CategoryCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(100);
        RuleFor(c => c.Slug).MaximumLength(100)
            .Matches("^[a-z0-9]+(-[a-z0-9]+)*$").When(c => !string.IsNullOrWhiteSpace(c.Slug))
            .WithMessage("Solo minúsculas, números y guiones (p. ej. hogar-y-cocina).");
        RuleFor(c => c.Description).MaximumLength(1000);
        RuleFor(c => c.ImageUrl).MaximumLength(1000);
        RuleFor(c => c.SortOrder).GreaterThanOrEqualTo(0);
    }
}

internal static class CategoryRules
{
    public static async Task<Result<string>> ValidateAsync(AppDbContext db, CategoryCommand command, Guid? id, CancellationToken ct)
    {
        var slug = Slug.From(string.IsNullOrWhiteSpace(command.Slug) ? command.Name : command.Slug);
        if (slug.Length == 0)
        {
            return Error.Validation("Category.InvalidSlug", "El nombre debe contener letras o números.");
        }

        if (await db.Categories.AnyAsync(c => c.Slug == slug && c.Id != id, ct))
        {
            return Error.Conflict("Category.DuplicateSlug", $"Ya existe una categoría con el identificador «{slug}».");
        }

        if (command.ParentId is { } parentId)
        {
            // Recorre los ancestros del padre propuesto: no puede existir ni incluir a la propia categoría.
            var parents = await db.Categories.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.ParentId, ct);
            if (!parents.ContainsKey(parentId))
            {
                return Error.Validation("Category.ParentNotFound", "La categoría padre no existe.");
            }

            for (Guid? current = parentId; current is { } node; current = parents.GetValueOrDefault(node))
            {
                if (node == id)
                {
                    return Error.Validation("Category.Cycle", "Una categoría no puede ser padre de sí misma ni de sus antecesoras.");
                }
            }
        }

        return slug;
    }

    public static void Apply(Category category, CategoryCommand command, string slug)
    {
        category.Name = command.Name.Trim();
        category.Slug = slug;
        category.Description = string.IsNullOrWhiteSpace(command.Description) ? null : command.Description.Trim();
        category.ImageUrl = string.IsNullOrWhiteSpace(command.ImageUrl) ? null : command.ImageUrl.Trim();
        category.ParentId = command.ParentId;
        category.SortOrder = command.SortOrder;
        category.IsActive = command.IsActive;
    }

    public static Task<CategoryResponse> LoadAsync(AppDbContext db, Guid id, CancellationToken ct) =>
        Query(db, c => c.Id == id).FirstAsync(ct);

    /// <summary>El filtro se aplica sobre la entidad: EF no puede traducir un Where posterior a la proyección.</summary>
    public static IQueryable<CategoryResponse> Query(AppDbContext db, Expression<Func<Category, bool>> filter) =>
        db.Categories.AsNoTracking()
            .Where(filter)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new CategoryResponse(c.Id, c.Name, c.Slug, c.Description, c.ImageUrl, c.ParentId,
                c.SortOrder, c.IsActive, db.Products.Count(p => p.CategoryId == c.Id)));
}

/// <summary>GET /api/categories?includeInactive= — público; la tienda lo usa para el menú de categorías.</summary>
public static class GetCategories
{
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapGet("/categories", async (bool? includeInactive, System.Security.Claims.ClaimsPrincipal user, Handler handler, CancellationToken ct) =>
                    TypedResults.Ok(await handler.HandleAsync(includeInactive == true && user.IsCatalogManager(), ct)))
                .WithTags("Catalog")
                .WithName("GetCategories");
    }

    public sealed class Handler(AppDbContext db) : IHandler
    {
        public async Task<IReadOnlyList<CategoryResponse>> HandleAsync(bool includeInactive, CancellationToken ct) =>
            await CategoryRules.Query(db, c => includeInactive || c.IsActive).ToListAsync(ct);
    }
}

/// <summary>POST /api/categories</summary>
public static class CreateCategory
{
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapPost("/categories", async (CategoryCommand command, Handler handler, CancellationToken ct) =>
                    (await handler.HandleAsync(command, ct)).Match(c => TypedResults.Created($"/api/categories/{c.Id}", c)))
                .WithValidation<CategoryCommand>()
                .RequireCatalogManager()
                .WithTags("Catalog")
                .WithName("CreateCategory")
                .Produces<CategoryResponse>(StatusCodes.Status201Created)
                .ProducesProblem(StatusCodes.Status409Conflict);
    }

    public sealed class Handler(AppDbContext db) : IHandler
    {
        public async Task<Result<CategoryResponse>> HandleAsync(CategoryCommand command, CancellationToken ct)
        {
            var slug = await CategoryRules.ValidateAsync(db, command, null, ct);
            if (!slug.IsSuccess)
            {
                return slug.Error!;
            }

            var category = new Category { Name = command.Name, Slug = slug.Value! };
            CategoryRules.Apply(category, command, slug.Value!);
            db.Categories.Add(category);
            await db.SaveChangesAsync(ct);

            return await CategoryRules.LoadAsync(db, category.Id, ct);
        }
    }
}

/// <summary>PUT /api/categories/{id}</summary>
public static class UpdateCategory
{
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapPut("/categories/{id:guid}", async (Guid id, CategoryCommand command, Handler handler, CancellationToken ct) =>
                    (await handler.HandleAsync(id, command, ct)).Match(TypedResults.Ok))
                .WithValidation<CategoryCommand>()
                .RequireCatalogManager()
                .WithTags("Catalog")
                .WithName("UpdateCategory")
                .Produces<CategoryResponse>()
                .ProducesProblem(StatusCodes.Status404NotFound)
                .ProducesProblem(StatusCodes.Status409Conflict);
    }

    public sealed class Handler(AppDbContext db) : IHandler
    {
        public async Task<Result<CategoryResponse>> HandleAsync(Guid id, CategoryCommand command, CancellationToken ct)
        {
            var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct);
            if (category is null)
            {
                return Error.NotFound("Category.NotFound", $"No existe la categoría {id}.");
            }

            var slug = await CategoryRules.ValidateAsync(db, command, id, ct);
            if (!slug.IsSuccess)
            {
                return slug.Error!;
            }

            CategoryRules.Apply(category, command, slug.Value!);
            await db.SaveChangesAsync(ct);

            return await CategoryRules.LoadAsync(db, id, ct);
        }
    }
}

/// <summary>DELETE /api/categories/{id} — solo si no tiene productos ni subcategorías (409 en caso contrario).</summary>
public static class DeleteCategory
{
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapDelete("/categories/{id:guid}", async (Guid id, Handler handler, CancellationToken ct) =>
                    (await handler.HandleAsync(id, ct)).Match(_ => TypedResults.NoContent()))
                .RequireCatalogManager()
                .WithTags("Catalog")
                .WithName("DeleteCategory")
                .ProducesProblem(StatusCodes.Status404NotFound)
                .ProducesProblem(StatusCodes.Status409Conflict);
    }

    public sealed class Handler(AppDbContext db) : IHandler
    {
        public async Task<Result<bool>> HandleAsync(Guid id, CancellationToken ct)
        {
            var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct);
            if (category is null)
            {
                return Error.NotFound("Category.NotFound", $"No existe la categoría {id}.");
            }

            var productCount = await db.Products.CountAsync(p => p.CategoryId == id, ct);
            if (productCount > 0)
            {
                return Error.Conflict("Category.HasProducts",
                    $"La categoría tiene {productCount} producto(s). Muévelos a otra categoría o desactívala.");
            }

            if (await db.Categories.AnyAsync(c => c.ParentId == id, ct))
            {
                return Error.Conflict("Category.HasChildren", "La categoría tiene subcategorías. Elimínalas o muévelas antes.");
            }

            db.Categories.Remove(category);
            await db.SaveChangesAsync(ct);
            return true;
        }
    }
}
