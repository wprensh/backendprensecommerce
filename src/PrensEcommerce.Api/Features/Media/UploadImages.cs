using Microsoft.Extensions.Options;
using PrensEcommerce.Api.Common.Abstractions;
using PrensEcommerce.Api.Common.Errors;
using PrensEcommerce.Api.Features.Catalog;
using PrensEcommerce.Api.Infrastructure.Media;
using PrensEcommerce.Api.Infrastructure.MultiTenancy;

namespace PrensEcommerce.Api.Features.Media;

/// <summary>
/// POST /api/admin/media/images — sube una o varias imágenes (multipart, campo «files») y devuelve sus URL.
/// Solo JPEG, PNG, WebP y GIF, comprobados por su firma binaria, no por la extensión.
/// </summary>
public static class UploadImages
{
    public sealed record Response(IReadOnlyList<string> Urls);

    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapPost("/admin/media/images", async (IFormFileCollection files, Handler handler, CancellationToken ct) =>
                    (await handler.HandleAsync(files, ct)).Match(TypedResults.Ok))
                .RequireCatalogManager()
                .WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(60 * 1024 * 1024))
                .DisableAntiforgery() // API con JWT en cabecera: no hay cookies que proteger.
                .WithTags("Media")
                .WithName("UploadImages");
    }

    /// <summary>POST /api/admin/media/campaign-images — igual, para los banners de campañas (catálogo o marketing).</summary>
    public sealed class CampaignEndpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapPost("/admin/media/campaign-images", async (IFormFileCollection files, Handler handler, CancellationToken ct) =>
                    (await handler.HandleAsync(files, ct, folder: "campaigns")).Match(TypedResults.Ok))
                .RequireAuthorization(Common.Security.AppPolicies.CampaignManager)
                .WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(60 * 1024 * 1024))
                .DisableAntiforgery()
                .WithTags("Media")
                .WithName("UploadCampaignImages");
    }

    public sealed class Handler(IMediaStorage storage, ITenantProvider tenant, IOptions<MediaOptions> options) : IHandler
    {
        public const int MaxFilesPerRequest = ProductRules.MaxImages;

        public async Task<Result<Response>> HandleAsync(IFormFileCollection files, CancellationToken ct, string folder = "products")
        {
            if (files.Count == 0)
            {
                return Error.Validation("Media.Empty", "No se recibió ninguna imagen.");
            }

            if (files.Count > MaxFilesPerRequest)
            {
                return Error.Validation("Media.TooMany", $"Sube como máximo {MaxFilesPerRequest} imágenes a la vez.");
            }

            var maxBytes = options.Value.MaxImageBytes;
            var prepared = new List<(IFormFile File, string Extension)>();
            foreach (var file in files)
            {
                if (file.Length == 0 || file.Length > maxBytes)
                {
                    return Error.Validation("Media.Size",
                        $"«{file.FileName}» supera el máximo de {maxBytes / (1024 * 1024)} MB o está vacía.");
                }

                await using var stream = file.OpenReadStream();
                var header = new byte[12];
                var read = await stream.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, ct);
                if (ImageSignature.Detect(header.AsSpan(0, read)) is not { } extension)
                {
                    return Error.Validation("Media.Type", $"«{file.FileName}» no es una imagen JPG, PNG, WebP o GIF.");
                }

                prepared.Add((file, extension));
            }

            var urls = new List<string>(prepared.Count);
            foreach (var (file, extension) in prepared)
            {
                await using var stream = file.OpenReadStream();
                urls.Add(await storage.SaveAsync(tenant.TenantId, folder, extension, stream, ct));
            }

            return new Response(urls);
        }
    }
}

/// <summary>Identifica el formato por los primeros bytes del archivo.</summary>
public static class ImageSignature
{
    public static string? Detect(ReadOnlySpan<byte> header) => header switch
    {
        [0xFF, 0xD8, 0xFF, ..] => ".jpg",
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, ..] => ".png",
        [0x47, 0x49, 0x46, 0x38, ..] => ".gif",
        [0x52, 0x49, 0x46, 0x46, _, _, _, _, 0x57, 0x45, 0x42, 0x50, ..] => ".webp",
        _ => null,
    };
}
