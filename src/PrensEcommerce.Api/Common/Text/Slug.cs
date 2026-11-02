using System.Text;

namespace PrensEcommerce.Api.Common.Text;

public static class Slug
{
    private static readonly Dictionary<char, char> Accents = BuildAccents();

    private static Dictionary<char, char> BuildAccents()
    {
        var map = new Dictionary<char, char>();
        foreach (var (plain, accented) in new[]
                 {
                     ('a', "áàäâãå"), ('e', "éèëê"), ('i', "íìïî"), ('o', "óòöôõ"),
                     ('u', "úùüû"), ('n', "ñ"), ('c', "ç"), ('y', "ýÿ"),
                 })
        {
            foreach (var ch in accented)
            {
                map[ch] = plain;
            }
        }

        return map;
    }

    /// <summary>"Hogar y Cocina" → "hogar-y-cocina". Quita tildes y caracteres no alfanuméricos.</summary>
    public static string From(string value)
    {
        // El proyecto usa InvariantGlobalization, así que string.Normalize no quita tildes: se mapean a mano.
        var lower = value.Trim().ToLowerInvariant();
        var builder = new StringBuilder(lower.Length);
        var lastWasDash = true;

        foreach (var raw in lower)
        {
            var c = Accents.TryGetValue(raw, out var plain) ? plain : raw;
            if (char.IsAsciiLetterOrDigit(c))
            {
                builder.Append(c);
                lastWasDash = false;
            }
            else if (!lastWasDash)
            {
                builder.Append('-');
                lastWasDash = true;
            }
        }

        return builder.ToString().TrimEnd('-');
    }
}
