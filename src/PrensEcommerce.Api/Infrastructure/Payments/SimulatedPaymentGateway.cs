using Microsoft.Extensions.Options;
using PrensEcommerce.Api.Domain;

namespace PrensEcommerce.Api.Infrastructure.Payments;

/// <summary>
/// Pasarela de pruebas: no mueve dinero. Imita el comportamiento de un proveedor real con
/// tokens de prueba que genera el frontend (como las tarjetas de test de Stripe o Mercado Pago):
/// <list type="bullet">
/// <item><c>tok_sim_declined</c> → tarjeta rechazada.</item>
/// <item><c>tok_sim_insufficient_funds</c> → fondos insuficientes.</item>
/// <item>cualquier otro <c>tok_sim_*</c> → cobro aprobado.</item>
/// </list>
/// Transferencia y contra entrega quedan en <see cref="PaymentStatus.Pending"/>.
/// </summary>
public sealed class SimulatedPaymentGateway(IOptions<PaymentOptions> options, ILogger<SimulatedPaymentGateway> logger)
    : IPaymentGateway
{
    public const string ProviderName = "simulated";
    public const string TokenPrefix = "tok_sim_";

    public string Name => ProviderName;

    public Task<PaymentResult> ChargeAsync(PaymentRequest request, CancellationToken ct)
    {
        var reference = $"sim_{Guid.CreateVersion7():N}";

        var result = request.Method switch
        {
            PaymentMethod.Transfer => new PaymentResult(
                PaymentStatus.Pending, reference,
                Instructions: $"{options.Value.TransferInstructions} Concepto: {request.OrderNumber}."),
            PaymentMethod.Cash => new PaymentResult(
                PaymentStatus.Pending, reference,
                Instructions: "Pagarás al recibir el pedido."),
            _ => ChargeCard(request.Token, reference),
        };

        logger.LogInformation(
            "Cobro simulado {Reference} de {Amount} {Currency} para el pedido {OrderNumber}: {Status}",
            reference, request.Amount, request.Currency, request.OrderNumber, result.Status);

        return Task.FromResult(result);
    }

    public Task RefundAsync(string providerReference, decimal amount, string currency, CancellationToken ct)
    {
        logger.LogWarning("Reembolso simulado de {Amount} {Currency} ({Reference})", amount, currency, providerReference);
        return Task.CompletedTask;
    }

    private static PaymentResult ChargeCard(string? token, string reference) => token switch
    {
        null or "" => new PaymentResult(PaymentStatus.Failed, null, "Falta el token de la tarjeta."),
        _ when !token.StartsWith(TokenPrefix, StringComparison.Ordinal) =>
            new PaymentResult(PaymentStatus.Failed, null, "Token de tarjeta no válido."),
        TokenPrefix + "declined" => new PaymentResult(PaymentStatus.Failed, reference, "La tarjeta fue rechazada por el banco emisor."),
        TokenPrefix + "insufficient_funds" => new PaymentResult(PaymentStatus.Failed, reference, "Fondos insuficientes."),
        _ => new PaymentResult(PaymentStatus.Captured, reference),
    };
}
