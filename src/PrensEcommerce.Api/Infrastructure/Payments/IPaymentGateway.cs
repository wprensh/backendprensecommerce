using PrensEcommerce.Api.Domain;

namespace PrensEcommerce.Api.Infrastructure.Payments;

/// <summary>
/// Pasarela de pago enchufable. Cada proveedor (simulado, Mercado Pago, Stripe…) es una
/// implementación; se elige con <c>Payments:Provider</c> en la configuración.
/// </summary>
/// <remarks>
/// El frontend tokeniza la tarjeta con el SDK del proveedor y solo envía el token:
/// el número de tarjeta nunca pasa por nuestra API (alcance PCI SAQ-A).
/// </remarks>
public interface IPaymentGateway
{
    string Name { get; }

    Task<PaymentResult> ChargeAsync(PaymentRequest request, CancellationToken ct);

    Task RefundAsync(string providerReference, decimal amount, string currency, CancellationToken ct);
}

public sealed record PaymentRequest(
    Guid OrderId,
    string OrderNumber,
    decimal Amount,
    string Currency,
    PaymentMethod Method,
    string? Token);

/// <param name="Instructions">Texto para el cliente cuando el pago queda pendiente (p. ej. datos de transferencia).</param>
public sealed record PaymentResult(
    PaymentStatus Status,
    string? ProviderReference,
    string? FailureReason = null,
    string? Instructions = null)
{
    public bool IsFailure => Status == PaymentStatus.Failed;
}
