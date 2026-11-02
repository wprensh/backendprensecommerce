using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PrensEcommerce.Api.Domain;

namespace PrensEcommerce.Api.Infrastructure.Persistence.Configurations;

internal sealed class LoyaltyAccountConfiguration : IEntityTypeConfiguration<LoyaltyAccount>
{
    public void Configure(EntityTypeBuilder<LoyaltyAccount> builder)
    {
        builder.ToTable("loyalty_accounts", Schemas.Crm);
        builder.Property(a => a.Tier).HasConversion<string>().HasMaxLength(20);
        builder.HasOne<User>().WithOne().HasForeignKey<LoyaltyAccount>(a => a.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(a => new { a.TenantId, a.UserId }).IsUnique();
        builder.HasMany(a => a.Transactions).WithOne().HasForeignKey(t => t.AccountId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class LoyaltyTransactionConfiguration : IEntityTypeConfiguration<LoyaltyTransaction>
{
    public void Configure(EntityTypeBuilder<LoyaltyTransaction> builder)
    {
        builder.ToTable("loyalty_transactions", Schemas.Crm);
        builder.Property(t => t.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.Reason).HasMaxLength(200);
        builder.HasOne<Order>().WithMany().HasForeignKey(t => t.OrderId).OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class CustomerNoteConfiguration : IEntityTypeConfiguration<CustomerNote>
{
    public void Configure(EntityTypeBuilder<CustomerNote> builder)
    {
        builder.ToTable("customer_notes", Schemas.Crm);
        builder.Property(n => n.Body).HasMaxLength(4000);
        builder.HasOne<User>().WithMany().HasForeignKey(n => n.CustomerId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>().WithMany().HasForeignKey(n => n.AuthorId).OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class AutomationRuleConfiguration : IEntityTypeConfiguration<AutomationRule>
{
    public void Configure(EntityTypeBuilder<AutomationRule> builder)
    {
        builder.ToTable("automation_rules", Schemas.Crm);
        builder.Property(r => r.Name).HasMaxLength(200);
        builder.Property(r => r.Trigger).HasConversion<string>().HasMaxLength(30);
        builder.Property(r => r.Action).HasConversion<string>().HasMaxLength(30);
        builder.Property(r => r.Configuration).HasColumnType("jsonb");
        builder.HasIndex(r => new { r.TenantId, r.Trigger, r.IsActive });
    }
}

internal sealed class AutomationRunConfiguration : IEntityTypeConfiguration<AutomationRun>
{
    public void Configure(EntityTypeBuilder<AutomationRun> builder)
    {
        builder.ToTable("automation_runs", Schemas.Crm);
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.Payload).HasColumnType("jsonb");
        builder.Property(r => r.Error).HasMaxLength(2000);
        builder.HasOne<AutomationRule>().WithMany().HasForeignKey(r => r.RuleId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(r => new { r.TenantId, r.Status });
    }
}
