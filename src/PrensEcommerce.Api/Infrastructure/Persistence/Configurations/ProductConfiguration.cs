using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PrensEcommerce.Api.Domain;

namespace PrensEcommerce.Api.Infrastructure.Persistence.Configurations;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.Property(p => p.Name).HasMaxLength(200);
        builder.Property(p => p.Description).HasMaxLength(4000);
        builder.Property(p => p.Category).HasMaxLength(50);
        builder.Property(p => p.ImageUrl).HasMaxLength(1000);
        builder.Property(p => p.Price).HasPrecision(12, 2);
        builder.Property(p => p.Rating).HasPrecision(2, 1);
        builder.Ignore(p => p.FinalPrice);

        builder.HasIndex(p => new { p.TenantId, p.Category });
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_products_discount", "discount_percent BETWEEN 0 AND 100");
            t.HasCheckConstraint("ck_products_stock", "stock >= 0");
        });
    }
}
