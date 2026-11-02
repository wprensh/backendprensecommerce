using System.Globalization;
using System.Security.Cryptography;

namespace PrensEcommerce.Api.Features.Checkout;

/// <summary>Genera números de pedido legibles: <c>PR-yyyyMMdd-XXXXXX</c> (sin 0/O ni 1/I).</summary>
public static class OrderNumber
{
    private const string Alphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";

    public static string New(DateTimeOffset now) =>
        $"PR-{now.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}-{RandomNumberGenerator.GetString(Alphabet, 6)}";
}
