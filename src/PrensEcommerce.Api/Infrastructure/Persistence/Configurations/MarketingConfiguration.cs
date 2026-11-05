using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PrensEcommerce.Api.Domain;

namespace PrensEcommerce.Api.Infrastructure.Persistence.Configurations;

internal sealed class CampaignConfiguration : IEntityTypeConfiguration<Campaign>
{
    public void Configure(EntityTypeBuilder<Campaign> builder)
    {
        builder.ToTable("campaigns", Schemas.Crm, t =>
            t.HasCheckConstraint("ck_campaigns_dates", "starts_at IS NULL OR ends_at IS NULL OR ends_at > starts_at"));

        builder.Property(c => c.Name).HasMaxLength(120);
        builder.Property(c => c.Eyebrow).HasMaxLength(60);
        builder.Property(c => c.Title).HasMaxLength(120);
        builder.Property(c => c.Subtitle).HasMaxLength(240);
        builder.Property(c => c.CtaLabel).HasMaxLength(40);
        builder.Property(c => c.ImageUrl).HasMaxLength(1000);
        builder.Property(c => c.ImageAlt).HasMaxLength(200);
        builder.Property(c => c.CategorySlug).HasMaxLength(100);
        builder.Property(c => c.Theme).HasConversion<string>().HasMaxLength(20);

        // Búsqueda de la campaña vigente del banner.
        builder.HasIndex(c => new { c.TenantId, c.IsActive, c.Priority });
    }
}
