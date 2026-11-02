using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PrensEcommerce.Api.Domain;

namespace PrensEcommerce.Api.Infrastructure.Persistence.Configurations;

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories", Schemas.Catalog);
        builder.Property(c => c.Name).HasMaxLength(100);
        builder.Property(c => c.Slug).HasMaxLength(100);
        builder.Property(c => c.Description).HasMaxLength(1000);
        builder.Property(c => c.ImageUrl).HasMaxLength(1000);
        builder.HasIndex(c => new { c.TenantId, c.Slug }).IsUnique();
        builder.HasOne<Category>().WithMany().HasForeignKey(c => c.ParentId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class BrandConfiguration : IEntityTypeConfiguration<Brand>
{
    public void Configure(EntityTypeBuilder<Brand> builder)
    {
        builder.ToTable("brands", Schemas.Catalog);
        builder.Property(b => b.Name).HasMaxLength(100);
        builder.Property(b => b.Slug).HasMaxLength(100);
        builder.Property(b => b.LogoUrl).HasMaxLength(1000);
        builder.HasIndex(b => new { b.TenantId, b.Slug }).IsUnique();
    }
}

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products", Schemas.Catalog, t =>
        {
            t.HasCheckConstraint("ck_products_discount", "discount_percent BETWEEN 0 AND 100");
            t.HasCheckConstraint("ck_products_stock", "stock >= 0");
            t.HasCheckConstraint("ck_products_price", "price >= 0");
        });

        builder.Property(p => p.Sku).HasMaxLength(64);
        builder.Property(p => p.Name).HasMaxLength(200);
        builder.Property(p => p.Description).HasMaxLength(4000);
        builder.Property(p => p.ImageUrl).HasMaxLength(1000);
        builder.Property(p => p.Price).HasPrecision(12, 2);
        builder.Property(p => p.Rating).HasPrecision(2, 1);
        builder.Ignore(p => p.FinalPrice);

        builder.HasOne(p => p.Category).WithMany().HasForeignKey(p => p.CategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.Brand).WithMany().HasForeignKey(p => p.BrandId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(p => new { p.TenantId, p.Sku }).IsUnique().HasFilter("sku IS NOT NULL");
        builder.HasIndex(p => new { p.TenantId, p.CategoryId });
    }
}
