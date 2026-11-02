namespace PrensEcommerce.Api.Features.Crm;

public static class CustomerTags
{
    /// <summary>Etiquetas en minúsculas y sin espacios sobrantes: "VIP " y "vip" son la misma.</summary>
    public static string Normalize(string tag) => tag.Trim().ToLowerInvariant();

    public static List<string> Normalize(IEnumerable<string> tags) =>
        [.. tags.Where(t => !string.IsNullOrWhiteSpace(t)).Select(Normalize).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
}
