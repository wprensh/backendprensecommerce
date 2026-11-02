using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PrensEcommerce.Api.Domain;

namespace PrensEcommerce.Api.Infrastructure.Persistence.Configurations;

internal sealed class SupportTicketConfiguration : IEntityTypeConfiguration<SupportTicket>
{
    public void Configure(EntityTypeBuilder<SupportTicket> builder)
    {
        builder.ToTable("tickets", Schemas.Support, t =>
            t.HasCheckConstraint("ck_tickets_satisfaction", "satisfaction_score IS NULL OR satisfaction_score BETWEEN 1 AND 5"));

        builder.Ignore(t => t.Code);
        builder.Ignore(t => t.IsClosed);
        builder.Ignore(t => t.CanBeRated);
        builder.Property(t => t.Subject).HasMaxLength(200);
        builder.Property(t => t.Category).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.SatisfactionComment).HasMaxLength(1000);
        builder.Property(t => t.ContactEmail).HasMaxLength(320);
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.Priority).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.Channel).HasConversion<string>().HasMaxLength(20);

        builder.HasOne<User>().WithMany().HasForeignKey(t => t.CustomerId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<User>().WithMany().HasForeignKey(t => t.AssignedToId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<Order>().WithMany().HasForeignKey(t => t.OrderId).OnDelete(DeleteBehavior.SetNull);
        builder.HasMany(t => t.Messages).WithOne().HasForeignKey(m => m.TicketId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => new { t.TenantId, t.Status });
        builder.HasIndex(t => new { t.TenantId, t.Number }).IsUnique();
        builder.HasIndex(t => new { t.TenantId, t.CustomerId });
    }
}

internal sealed class TicketMessageConfiguration : IEntityTypeConfiguration<TicketMessage>
{
    public void Configure(EntityTypeBuilder<TicketMessage> builder)
    {
        builder.ToTable("ticket_messages", Schemas.Support);
        builder.Property(m => m.Body).HasMaxLength(8000);
        builder.HasOne<User>().WithMany().HasForeignKey(m => m.AuthorId).OnDelete(DeleteBehavior.SetNull);
    }
}
