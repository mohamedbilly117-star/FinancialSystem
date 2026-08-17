using ERP.Domain.Entities.Accounting.RuleEngine;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Persistence.Configurations.RuleEngine;

/// <summary>
/// Prompt 5 - Accounting Rule Engine. Unlike
/// <see cref="Distribution.DistributionTemplateConfiguration"/>, this
/// deliberately does NOT enforce "one Active rule per SourceModuleCode" -
/// multiple Active rules per module is the intended design (a general
/// rule plus one or more narrower Exception rules), disambiguated by
/// <see cref="AccountingRule.Priority"/> and
/// <see cref="AccountingRuleResolver"/> at resolution time. What IS
/// enforced at the database level is the narrower, genuinely-invalid case:
/// two Active rules for the same module sharing the exact same Priority
/// value, which <see cref="AccountingRuleResolver"/> could never
/// deterministically break a tie on.
/// </summary>
public sealed class AccountingRuleConfiguration : IEntityTypeConfiguration<AccountingRule>
{
    public void Configure(EntityTypeBuilder<AccountingRule> builder)
    {
        builder.ToTable("AccountingRules", schema: "accounting");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.SourceModuleCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Code).HasMaxLength(30).IsRequired();
        builder.Property(x => x.NameAr).HasMaxLength(150).IsRequired();
        builder.Property(x => x.NameEn).HasMaxLength(150).IsRequired();

        builder.Property(x => x.Priority).IsRequired();
        builder.Property(x => x.IsException).IsRequired();

        builder.Property(x => x.DebitDistributionSourceType).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.CreditDistributionSourceType).HasConversion<string>().HasMaxLength(20);

        builder.Property(x => x.RequiresApprovalBeforePosting).IsRequired();

        builder.Property(x => x.Version).IsRequired();
        builder.Property(x => x.EffectiveFrom).HasColumnType("date").IsRequired();
        builder.Property(x => x.EffectiveTo).HasColumnType("date");

        builder.Property(x => x.IsActive).IsRequired();

        builder.Property(x => x.IsDeleted).IsRequired();
        builder.Property(x => x.DeletedAtUtc);

        builder.HasOne<ERP.Domain.Entities.Accounting.Account>()
            .WithMany()
            .HasForeignKey(x => x.DebitAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ERP.Domain.Entities.Accounting.Account>()
            .WithMany()
            .HasForeignKey(x => x.CreditAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        // Prevents an undecidable resolver tie: two Active rules for the
        // same module can never share a Priority value.
        builder.HasIndex(x => new { x.SourceModuleCode, x.Priority, x.IsActive })
            .IsUnique()
            .HasFilter("[IsActive] = 1")
            .HasDatabaseName("IX_AccountingRules_ActivePriority");

        builder.HasIndex(x => new { x.SourceModuleCode, x.Version });

        builder.HasMany(x => x.Conditions)
            .WithOne()
            .HasForeignKey(c => c.AccountingRuleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Conditions)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // MatchConditions/Exceptions are computed LINQ projections over
        // Conditions (Prompt-driven Kind filter), never physical columns.
        builder.Ignore(x => x.MatchConditions);
        builder.Ignore(x => x.Exceptions);
    }
}

public sealed class AccountingRuleConditionConfiguration : IEntityTypeConfiguration<AccountingRuleCondition>
{
    public void Configure(EntityTypeBuilder<AccountingRuleCondition> builder)
    {
        builder.ToTable("AccountingRuleConditions", schema: "accounting");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.AccountingRuleId).IsRequired();

        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.Property(x => x.FieldName).HasMaxLength(100).IsRequired();

        builder.Property(x => x.Operator).HasConversion<string>().HasMaxLength(20).IsRequired();

        // Value/ValueTo are stored as strings deliberately - a condition
        // may compare a text field (Equals/NotEquals, e.g. OfficeCode) or
        // a numeric field (GreaterThan/Between, e.g. Amount); forcing a
        // single SQL column type would require either lossy numeric
        // storage of text values or a second nullable numeric column that
        // is empty half the time. Numeric parsing happens in
        // AccountingRuleCondition.IsSatisfiedBy at evaluation time.
        builder.Property(x => x.Value).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ValueTo).HasMaxLength(200);

        builder.HasIndex(x => new { x.AccountingRuleId, x.FieldName });
    }
}
