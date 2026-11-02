using System.ComponentModel.DataAnnotations;

namespace PrensEcommerce.Api.Features.Checkout;

/// <summary>Reglas de precio del checkout (sección <c>Checkout</c> de appsettings).</summary>
public sealed class CheckoutOptions
{
    public const string SectionName = "Checkout";

    [Required, StringLength(3, MinimumLength = 3)]
    public string Currency { get; set; } = "COP";

    /// <summary>Coste de envío fijo (en la moneda de la tienda; COP por defecto).</summary>
    [Range(0, 10_000_000)]
    public decimal ShippingCost { get; set; } = 15_000m;

    /// <summary>Subtotal a partir del cual el envío es gratis. <c>null</c> = nunca.</summary>
    [Range(0, 1_000_000_000)]
    public decimal? FreeShippingFrom { get; set; }

    public decimal ShippingFor(decimal subtotal) =>
        FreeShippingFrom is { } threshold && subtotal >= threshold ? 0m : ShippingCost;
}
