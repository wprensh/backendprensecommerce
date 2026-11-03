using PrensEcommerce.Api.Common.Text;

namespace PrensEcommerce.Api.Tests.Catalog;

public sealed class SlugTests
{
    [Theory]
    [InlineData("Hogar y Cocina", "hogar-y-cocina")]
    [InlineData("  Electrónica  & Señales ", "electronica-senales")]
    [InlineData("Sony Électronique", "sony-electronique")]
    [InlineData("--audio--", "audio")]
    [InlineData("!!!", "")]
    public void From_GeneraSlugSinTildesNiSimbolos(string input, string expected) =>
        Assert.Equal(expected, Slug.From(input));
}
