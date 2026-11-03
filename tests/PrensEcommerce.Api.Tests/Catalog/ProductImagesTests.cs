using PrensEcommerce.Api.Features.Catalog;
using PrensEcommerce.Api.Features.Catalog.CreateProduct;
using PrensEcommerce.Api.Features.Media;

namespace PrensEcommerce.Api.Tests.Catalog;

public sealed class ProductImagesTests
{
    private static CreateProduct.Command Command(string? imageUrl, IReadOnlyList<string>? images) =>
        new("Producto", null, null, 1000, 0, Guid.NewGuid(), null, imageUrl, 1) { Images = images };

    [Fact]
    public void Gallery_keeps_order_and_drops_blanks_and_duplicates()
    {
        var (images, main) = ProductRules.NormalizeImages(Command(null, [" /a.jpg ", "", "/b.png", "/a.jpg"]));
        Assert.Equal(["/a.jpg", "/b.png"], images);
        Assert.Equal("/a.jpg", main);
    }

    [Fact]
    public void Without_gallery_the_single_image_url_is_used()
    {
        var (images, main) = ProductRules.NormalizeImages(Command("https://x/y.jpg", null));
        Assert.Equal(["https://x/y.jpg"], images);
        Assert.Equal("https://x/y.jpg", main);
    }

    [Fact]
    public void Empty_gallery_clears_the_main_image()
    {
        var (images, main) = ProductRules.NormalizeImages(Command("https://x/y.jpg", []));
        Assert.Empty(images);
        Assert.Equal(string.Empty, main);
    }

    [Theory]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, ".jpg")]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, ".png")]
    [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 }, ".gif")]
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 1, 2, 3, 4, 0x57, 0x45, 0x42, 0x50 }, ".webp")]
    [InlineData(new byte[] { 0x3C, 0x73, 0x76, 0x67 }, null)] // <svg: rechazado (puede llevar scripts)
    public void Detects_image_type_from_its_signature(byte[] header, string? expected) =>
        Assert.Equal(expected, ImageSignature.Detect(header));
}
