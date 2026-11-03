using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

namespace PrensEcommerce.Api.Infrastructure.Media;

public sealed class MediaOptions
{
    public const string SectionName = "Media";

    /// <summary>Carpeta de archivos subidos (relativa a la raíz de la API o absoluta).</summary>
    public string RootPath { get; set; } = "media";

    /// <summary>Ruta pública bajo la que se sirven; va bajo /api para que el proxy de desarrollo la incluya.</summary>
    public string RequestPath { get; set; } = "/api/media";

    public long MaxImageBytes { get; set; } = 5 * 1024 * 1024;
}

/// <summary>
/// Almacenamiento de archivos de la tienda. La implementación local guarda en disco; para producción
/// se puede cambiar por S3, Azure Blob o Cloudinary sin tocar los slices.
/// </summary>
public interface IMediaStorage
{
    /// <returns>URL pública del archivo guardado.</returns>
    Task<string> SaveAsync(Guid tenantId, string folder, string extension, Stream content, CancellationToken ct);
}

internal sealed class LocalMediaStorage(IOptions<MediaOptions> options, IWebHostEnvironment env) : IMediaStorage
{
    public string Root => Path.GetFullPath(Path.Combine(env.ContentRootPath, options.Value.RootPath));

    public async Task<string> SaveAsync(Guid tenantId, string folder, string extension, Stream content, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        // Carpeta por tienda: un tenant nunca escribe en la de otro.
        var relative = Path.Combine(tenantId.ToString("N"), folder, now.ToString("yyyy"), now.ToString("MM"));
        var directory = Path.Combine(Root, relative);
        Directory.CreateDirectory(directory);

        var fileName = $"{Guid.CreateVersion7():N}{extension}";
        await using (var file = File.Create(Path.Combine(directory, fileName)))
        {
            await content.CopyToAsync(file, ct);
        }

        return $"{options.Value.RequestPath}/{relative.Replace('\\', '/')}/{fileName}";
    }
}

public static class MediaExtensions
{
    public static IServiceCollection AddMedia(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<MediaOptions>().Bind(configuration.GetSection(MediaOptions.SectionName));
        services.AddSingleton<LocalMediaStorage>();
        services.AddSingleton<IMediaStorage>(sp => sp.GetRequiredService<LocalMediaStorage>());
        return services;
    }

    /// <summary>Sirve los archivos subidos. Va antes de la resolución de tenant: las imágenes son públicas.</summary>
    public static IApplicationBuilder UseMedia(this WebApplication app)
    {
        var storage = app.Services.GetRequiredService<LocalMediaStorage>();
        Directory.CreateDirectory(storage.Root);
        return app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(storage.Root),
            RequestPath = app.Services.GetRequiredService<IOptions<MediaOptions>>().Value.RequestPath,
            OnPrepareResponse = ctx => ctx.Context.Response.Headers.CacheControl = "public,max-age=31536000,immutable",
        });
    }
}
