using ERP.Domain.Entities.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Persistence.Configurations.Accounting;

/// <summary>
/// Prompt 4 - Entity Specification for FiscalYear. Fluent API only (never
/// data annotations on the entity itself), keeping ERP.Domain free of any
/// EF Core dependency per Prompt 3's layering rule.
/// </summary>
public sealed class FiscalYearConfiguration : IEntityTypeConfiguration<FiscalYear>
{
    public void Configure(EntityTypeBuilder<FiscalYear> builder)
    {
        builder.ToTable("FiscalYears", schema: "accounting");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code)
            .HasMaxLength(20)
            .IsRequired();

        // Prompt 4 - "Unique Constraints": a fiscal year code must be unique across the whole system's history, never reused even after the year is archived.
        builder.HasIndex(x => x.Code)
            .IsUnique();

        builder.Property(x => x.NameAr).HasMaxLength(100).IsRequired();
        builder.Property(x => x.NameEn).HasMaxLength(100).IsRequired();

        builder.Property(x => x.StartDate).HasColumnType("date").IsRequired();
        builder.Property(x => x.EndDate).HasColumnType("date").IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        // Periods are only ever reached through their FiscalYear (child of the aggregate) - never a standalone DbSet write target, but EF Core still needs the relationship mapped.
        builder.HasMany(x => x.Periods)
            .WithOne()
            .HasForeignKey(p => p.FiscalYearId)
            .OnDelete(DeleteBehavior.Restrict); // Prompt 4: financial/history data is never cascade-deleted.

        builder.Navigation(x => x.Periods)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Prompt 4 - Audit Data: every AuditableEntity gets these via the base configuration convention applied in ApplicationDbContext.OnModelCreating; repeated explicitly per-entity here for clarity and to guarantee they are never accidentally omitted.
        builder.Property(x => x.CreatedBy).IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();
    }
}

/// <summary>
/// AccountingPeriod is a child entity of the FiscalYear aggregate (never
/// its own aggregate root - see the Domain-layer remarks on the class
/// itself) but is still given its own configuration and table, and remains
/// independently queryable (read-only from outside the aggregate) for
/// reporting/filtering performance (Prompt 4 - Indexing Strategy).
/// </summary>
public sealed class AccountingPeriodConfiguration : IEntityTypeConfiguration<AccountingPeriod>
{
    public void Configure(EntityTypeBuilder<AccountingPeriod> builder)
    {
        builder.ToTable("AccountingPeriods", schema: "accounting");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.FiscalYearId).IsRequired();

        builder.Property(x => x.PeriodNumber).IsRequired();

        // A given fiscal year cannot have two periods with the same number (Prompt 4 - Unique Constraints, scoped to the parent).
        builder.HasIndex(x => new { x.FiscalYearId, x.PeriodNumber }).IsUnique();

        builder.Property(x => x.NameAr).HasMaxLength(100).IsRequired();
        builder.Property(x => x.NameEn).HasMaxLength(100).IsRequired();

        builder.Property(x => x.StartDate).HasColumnType("date").IsRequired();
        builder.Property(x => x.EndDate).HasColumnType("date").IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.IsAdjustmentPeriod).IsRequired();

        // Every JournalEntry.AccountingPeriodId references this table - restrict, never cascade (Prompt 4: financial history is never lost as a side effect of an unrelated delete).
        builder.HasIndex(x => x.StartDate);
        builder.HasIndex(x => x.EndDate);
    }
}
