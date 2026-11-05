using FluentValidation;
using PrensEcommerce.Api.Domain;

namespace PrensEcommerce.Api.Features.Campaigns;

/// <summary>Estado calculado de una campaña respecto a la fecha actual.</summary>
public enum CampaignStatus
{
    /// <summary>Desactivada manualmente.</summary>
    Inactive,

    /// <summary>Activa pero aún no ha empezado.</summary>
    Scheduled,

    /// <summary>Vigente (puede que otra con más prioridad sea la que se muestra).</summary>
    Running,

    /// <summary>Ya terminó.</summary>
    Ended,
}

/// <summary>Lo que necesita el banner del catálogo (endpoint público).</summary>
public sealed record CampaignBannerResponse(
    Guid Id,
    string Eyebrow,
    string Title,
    string? Subtitle,
    string? CtaLabel,
    string? ImageUrl,
    string? ImageAlt,
    string? CategorySlug,
    CampaignTheme Theme,
    DateTimeOffset? EndsAt);

/// <summary>Campaña completa para la administración.</summary>
public sealed record CampaignResponse(
    Guid Id,
    string Name,
    string Eyebrow,
    string Title,
    string? Subtitle,
    string? CtaLabel,
    string? ImageUrl,
    string? ImageAlt,
    string? CategorySlug,
    CampaignTheme Theme,
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt,
    int Priority,
    bool IsActive,
    CampaignStatus Status,
    // Es la que se ve ahora mismo en el banner.
    bool IsShowing,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

/// <summary>Datos editables de una campaña (alta y modificación).</summary>
public sealed record CampaignCommand(
    string Name,
    string? Eyebrow,
    string Title,
    string? Subtitle,
    string? CtaLabel,
    string? ImageUrl,
    string? ImageAlt,
    string? CategorySlug,
    CampaignTheme Theme = CampaignTheme.Light,
    DateTimeOffset? StartsAt = null,
    DateTimeOffset? EndsAt = null,
    int Priority = 0,
    bool IsActive = true);

public sealed class CampaignCommandValidator : AbstractValidator<CampaignCommand>
{
    public CampaignCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(120);
        RuleFor(c => c.Eyebrow).MaximumLength(60);
        RuleFor(c => c.Title).NotEmpty().MaximumLength(120);
        RuleFor(c => c.Subtitle).MaximumLength(240);
        RuleFor(c => c.CtaLabel).MaximumLength(40);
        RuleFor(c => c.ImageUrl).MaximumLength(1000)
            .Must(BeHttpOrRelativeUrl).When(c => !string.IsNullOrWhiteSpace(c.ImageUrl))
            .WithMessage("La imagen debe ser una URL http(s) o una ruta /api/media/….");
        RuleFor(c => c.ImageAlt).MaximumLength(200);
        RuleFor(c => c.CategorySlug).MaximumLength(100);
        RuleFor(c => c.Theme).IsInEnum();
        RuleFor(c => c.Priority).InclusiveBetween(0, 1000);
        RuleFor(c => c.EndsAt).GreaterThan(c => c.StartsAt)
            .When(c => c.StartsAt is not null && c.EndsAt is not null)
            .WithMessage("La fecha de fin debe ser posterior a la de inicio.");
    }

    private static bool BeHttpOrRelativeUrl(string? url) =>
        url!.StartsWith('/') ||
        (Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps));
}

public static class CampaignMapping
{
    public static CampaignStatus StatusAt(this Campaign c, DateTimeOffset now) =>
        !c.IsActive ? CampaignStatus.Inactive
        : c.EndsAt is not null && c.EndsAt <= now ? CampaignStatus.Ended
        : c.StartsAt is not null && c.StartsAt > now ? CampaignStatus.Scheduled
        : CampaignStatus.Running;

    public static CampaignResponse ToResponse(this Campaign c, DateTimeOffset now, Guid? showingId) => new(
        c.Id, c.Name, c.Eyebrow, c.Title, c.Subtitle, c.CtaLabel, c.ImageUrl, c.ImageAlt, c.CategorySlug, c.Theme,
        c.StartsAt, c.EndsAt, c.Priority, c.IsActive, c.StatusAt(now), c.Id == showingId, c.CreatedAt, c.UpdatedAt);

    public static CampaignBannerResponse ToBanner(this Campaign c) => new(
        c.Id, c.Eyebrow, c.Title, c.Subtitle, c.CtaLabel, c.ImageUrl, c.ImageAlt, c.CategorySlug, c.Theme, c.EndsAt);

    /// <summary>Normaliza el comando sobre la entidad (recorta textos y convierte vacíos en null).</summary>
    public static void Apply(this Campaign c, CampaignCommand command)
    {
        c.Name = command.Name.Trim();
        c.Eyebrow = command.Eyebrow?.Trim() ?? string.Empty;
        c.Title = command.Title.Trim();
        c.Subtitle = NullIfEmpty(command.Subtitle);
        c.CtaLabel = NullIfEmpty(command.CtaLabel);
        c.ImageUrl = NullIfEmpty(command.ImageUrl);
        c.ImageAlt = NullIfEmpty(command.ImageAlt);
        c.CategorySlug = NullIfEmpty(command.CategorySlug)?.ToLowerInvariant();
        c.Theme = command.Theme;
        c.StartsAt = command.StartsAt;
        c.EndsAt = command.EndsAt;
        c.Priority = command.Priority;
        c.IsActive = command.IsActive;
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
