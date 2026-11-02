using System.Security.Claims;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using PrensEcommerce.Api.Common.Abstractions;
using PrensEcommerce.Api.Common.Errors;
using PrensEcommerce.Api.Common.Security;
using PrensEcommerce.Api.Common.Validation;
using PrensEcommerce.Api.Infrastructure.Persistence;

namespace PrensEcommerce.Api.Features.Cart.AddCartItem;

/// <summary>POST /api/cart/items — añade (o suma) un producto al carrito.</summary>
public static class AddCartItem
{
    public sealed record Command(Guid ProductId, int Quantity = 1);

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(c => c.ProductId).NotEmpty();
            RuleFor(c => c.Quantity).InclusiveBetween(1, 99);
        }
    }

    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapPost("/cart/items", async (Command command, ClaimsPrincipal user, Handler handler, CancellationToken ct) =>
                    (await handler.HandleAsync(user.GetUserId(), command, ct)).Match(_ => TypedResults.NoContent()))
                .WithValidation<Command>()
                .RequireAuthorization()
                .WithTags("Cart")
                .WithName("AddCartItem");
    }

    public sealed class Handler(CartStore carts, AppDbContext db) : IHandler
    {
        public async Task<Result<bool>> HandleAsync(Guid userId, Command command, CancellationToken ct)
        {
            var stock = await db.Products
                .Where(p => p.Id == command.ProductId)
                .Select(p => (int?)p.Stock)
                .FirstOrDefaultAsync(ct);

            if (stock is null)
            {
                return Error.NotFound("Product.NotFound", "El producto no existe en esta tienda.");
            }

            var lines = await carts.GetAsync(userId, ct);
            var current = lines.Find(l => l.ProductId == command.ProductId);
            var quantity = (current?.Quantity ?? 0) + command.Quantity;

            if (quantity > stock)
            {
                return Error.Conflict("Cart.InsufficientStock", $"Solo hay {stock} unidades disponibles.");
            }

            lines.RemoveAll(l => l.ProductId == command.ProductId);
            lines.Add(new CartLine(command.ProductId, quantity));
            await carts.SaveAsync(userId, lines, ct);

            return true;
        }
    }
}
