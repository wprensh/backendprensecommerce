using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PrensEcommerce.Api.Domain;

namespace PrensEcommerce.Api.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.Property(u => u.Name).HasMaxLength(200);
        builder.Property(u => u.Email).HasMaxLength(320);
        builder.HasIndex(u => new { u.TenantId, u.Email }).IsUnique();
    }
}
