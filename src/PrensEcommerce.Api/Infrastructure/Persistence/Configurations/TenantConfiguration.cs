using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PrensEcommerce.Api.Domain;

namespace PrensEcommerce.Api.Infrastructure.Persistence.Configurations;

internal sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.Property(t => t.Slug).HasMaxLength(63);
        builder.Property(t => t.Name).HasMaxLength(200);
        builder.HasIndex(t => t.Slug).IsUnique();
    }
}
