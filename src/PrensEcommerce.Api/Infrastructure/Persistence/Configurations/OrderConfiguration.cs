using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PrensEcommerce.Api.Domain;

namespace PrensEcommerce.Api.Infrastructure.Persistence.Configurations;

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.Property(o => o.Total).HasPrecision(12, 2);
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(o => o.PaymentMethod).HasConversion<string>().HasMaxLength(20);

        builder.OwnsOne(o => o.ShippingAddress, a =>
        {
            a.Property(x => x.FullName).HasMaxLength(200);
            a.Property(x => x.Address).HasMaxLength(500);
            a.Property(x => x.City).HasMaxLength(100);
            a.Property(x => x.Phone).HasMaxLength(30);
        });

        builder.HasMany(o => o.Items).WithOne().HasForeignKey(i => i.OrderId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>().WithMany().HasForeignKey(o => o.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(o => new { o.TenantId, o.UserId });
    }
}

internal sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("order_items");
        builder.Property(i => i.ProductName).HasMaxLength(200);
        builder.Property(i => i.UnitPrice).HasPrecision(12, 2);
        builder.HasOne<Product>().WithMany().HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Restrict);
    }
}
