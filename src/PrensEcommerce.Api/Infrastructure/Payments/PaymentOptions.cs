using System.ComponentModel.DataAnnotations;

namespace PrensEcommerce.Api.Infrastructure.Payments;

public sealed class PaymentOptions
{
    public const string SectionName = "Payments";

    /// <summary>Pasarela activa (sin distinguir mayúsculas). Hoy solo existe <c>simulated</c>.</summary>
    [Required]
    public string Provider { get; set; } = SimulatedPaymentGateway.ProviderName;

    /// <summary>Datos que se muestran al cliente cuando paga por transferencia.</summary>
    public string TransferInstructions { get; set; } =
        "Transfiere el importe a la cuenta ES00 0000 0000 0000 0000 0000 indicando el número de pedido como concepto.";
}
