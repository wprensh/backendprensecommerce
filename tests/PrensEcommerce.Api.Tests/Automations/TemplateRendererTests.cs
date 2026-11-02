using PrensEcommerce.Api.Features.Automations.Engine;

namespace PrensEcommerce.Api.Tests.Automations;

public class TemplateRendererTests
{
    [Fact]
    public void Render_replaces_known_variables_case_insensitively_and_blanks_unknown_ones()
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase) { ["nombre"] = "Ana", ["tienda"] = "Prens" };

        var result = TemplateRenderer.Render("Hola {{ Nombre }}, bienvenida a {{tienda}}{{desconocida}}.", values);

        Assert.Equal("Hola Ana, bienvenida a Prens.", result);
    }

    [Fact]
    public void FormatMoney_uses_spanish_separators_even_with_invariant_globalization()
    {
        Assert.Equal("1.234,50", TemplateRenderer.FormatMoney(1234.5m));
        Assert.Equal(string.Empty, TemplateRenderer.FormatMoney(null));
    }

    [Fact]
    public void ShortReference_is_eight_uppercase_characters()
    {
        var id = Guid.Parse("01a19dd4-6438-7523-928b-b37fa490c4df");
        Assert.Equal("01A19DD4", TemplateRenderer.ShortReference(id));
    }

    [Fact]
    public void Every_default_template_only_uses_documented_variables()
    {
        foreach (var template in DefaultEmailTemplates.All)
        {
            var text = template.Subject + template.Body;
            var used = System.Text.RegularExpressions.Regex.Matches(text, @"\{\{\s*([a-z_]+)\s*\}\}").Select(m => m.Groups[1].Value);
            Assert.All(used, v => Assert.Contains(v, TemplateRenderer.Variables));
        }
    }
}
