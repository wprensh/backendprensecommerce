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
        builder.Property(p => p.Images).HasDefaultValueSql("'{}'::text[]");
        builder.Property(p => p.Price).HasPrecision(12, 2);
        builder.Property(p => p.Rating).HasPrecision(2, 1);
        builder.Ignore(p => p.FinalPrice);

        builder.HasOne(p => p.Category).WithMany().HasForeignKey(p => p.CategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.Brand).WithMany().HasForeignKey(p => p.BrandId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(p => new { p.TenantId, p.Sku }).IsUnique().HasFilter("sku IS NOT NULL");
        builder.HasIndex(p => new { p.TenantId, p.CategoryId });
    }
}

internal sealed class WishlistItemConfiguration : IEntityTypeConfiguration<WishlistItem>
{
    public void Configure(EntityTypeBuilder<WishlistItem> builder)
    {
        builder.ToTable("wishlist_items", Schemas.Catalog);
        builder.HasOne<User>().WithMany().HasForeignKey(w => w.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(w => w.Product).WithMany().HasForeignKey(w => w.ProductId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(w => new { w.TenantId, w.UserId, w.ProductId }).IsUnique();
    }
}

internal sealed class ProductReviewConfiguration : IEntityTypeConfiguration<ProductReview>
{
    public void Configure(EntityTypeBuilder<ProductReview> builder)
    {
        builder.ToTable("product_reviews", Schemas.Catalog);
        builder.Property(r => r.AuthorName).HasMaxLength(200);
        builder.Property(r => r.Title).HasMaxLength(150);
        builder.Property(r => r.Comment).HasMaxLength(2000);
        builder.HasOne<User>().WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Product>().WithMany().HasForeignKey(r => r.ProductId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(r => new { r.TenantId, r.ProductId, r.UserId }).IsUnique();
        builder.HasIndex(r => new { r.ProductId, r.CreatedAt });
    }
}
