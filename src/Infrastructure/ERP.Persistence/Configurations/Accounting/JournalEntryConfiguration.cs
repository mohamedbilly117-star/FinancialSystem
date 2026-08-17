using ERP.Domain.Entities.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Persistence.Configurations.Accounting;

/// <summary>
/// Prompt 4 / Prompt 5 - JournalEntry (the Automatic Journal Engine's
/// header table). <c>TotalDebit</c>/<c>TotalCredit</c> are deliberately
/// NOT mapped to columns - they are always computed in-memory from the
/// loaded <see cref="JournalEntry.Lines"/> (single source of truth,
/// impossible for a cached total to drift out of sync with its lines).
/// </summary>
public sealed class JournalEntryConfiguration : IEntityTypeConfiguration<JournalEntry>
{
    public void Configure(EntityTypeBuilder<JournalEntry> builder)
    {
        builder.ToTable("JournalEntries", schema: "accounting");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.JournalNumber).HasMaxLength(30);

        // Unique among assigned (non-null/non-empty) numbers only - Draft
        // entries that never reach Posted legitimately share a null value,
        // and SQL Server's default unique-index semantics already permit
        // multiple NULLs; the filter documents that intent explicitly.
        builder.HasIndex(x => x.JournalNumber)
            .IsUnique()
            .HasFilter("[JournalNumber] IS NOT NULL");

        builder.Property(x => x.FiscalYearId).IsRequired();
        builder.Property(x => x.AccountingPeriodId).IsRequired();

        builder.Property(x => x.EntryDate).HasColumnType("date").IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.DescriptionAr).HasMaxLength(500).IsRequired();
        builder.Property(x => x.DescriptionEn).HasMaxLength(500).IsRequired();

        builder.Property(x => x.SourceModuleCode).HasMaxLength(50);
        builder.Property(x => x.RejectionReason).HasMaxLength(500);

        builder.Property(x => x.ApprovedAtUtc);
        builder.Property(x => x.PostedAtUtc);

        // Computed, in-memory only - never persisted as columns.
        builder.Ignore(x => x.TotalDebit);
        builder.Ignore(x => x.TotalCredit);

        builder.Property(x => x.IsDeleted).IsRequired();
        builder.Property(x => x.DeletedAtUtc);

        builder.HasOne<FiscalYear>()
            .WithMany()
            .HasForeignKey(x => x.FiscalYearId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<AccountingPeriod>()
            .WithMany()
            .HasForeignKey(x => x.AccountingPeriodId)
            .OnDelete(DeleteBehavior.Restrict);

        // Self-referencing: a reversing entry points back at the original it reverses. No inverse navigation exposed - queried explicitly when needed.
        builder.HasOne<JournalEntry>()
            .WithMany()
            .HasForeignKey(x => x.ReversalOfJournalEntryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Lines)
            .WithOne()
            .HasForeignKey(l => l.JournalEntryId)
            .OnDelete(DeleteBehavior.Cascade); // Lines have no meaning without their JournalEntry and are only ever created/removed through it (Prompt 4 - the aggregate, not the individual line, is the unit of consistency); this cascade only ever fires for a hard delete of a still-Draft entry, since Posted entries are never physically deleted (ISoftDelete).

        builder.Navigation(x => x.Lines)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Prompt 4 - Indexing Strategy: these are exactly the filters every accounting report and the posting queue itself use (Prompt 9 - Report Filters: Fiscal Year, Accounting Period, Date Range, Status).
        builder.HasIndex(x => x.FiscalYearId);
        builder.HasIndex(x => x.AccountingPeriodId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.EntryDate);
        builder.HasIndex(x => new { x.SourceModuleCode, x.SourceReferenceId });
    }
}

/// <summary>
/// Prompt 4 - JournalEntryLine (the Automatic Journal Engine's detail
/// rows). Money amounts use decimal(18,4) - four decimal places
/// accommodate fractional-currency rounding scenarios (e.g. distribution
/// percentage splits, Prompt 11 addendum) without silently truncating a
/// value smaller than one cent/fils during intermediate calculations.
/// </summary>
public sealed class JournalEntryLineConfiguration : IEntityTypeConfiguration<JournalEntryLine>
{
    public void Configure(EntityTypeBuilder<JournalEntryLine> builder)
    {
        builder.ToTable("JournalEntryLines", schema: "accounting");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.JournalEntryId).IsRequired();
        builder.Property(x => x.LineNumber).IsRequired();

        builder.HasIndex(x => new { x.JournalEntryId, x.LineNumber }).IsUnique();

        builder.Property(x => x.AccountId).IsRequired();

        builder.Property(x => x.DebitAmount).HasPrecision(18, 4).IsRequired();
        builder.Property(x => x.CreditAmount).HasPrecision(18, 4).IsRequired();

        builder.Property(x => x.DescriptionAr).HasMaxLength(500);
        builder.Property(x => x.DescriptionEn).HasMaxLength(500);

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(x => x.AccountId)
            .OnDelete(DeleteBehavior.Restrict); // Prompt 4: an account referenced by any historical journal line can be deactivated, never deleted.

        // Prompt 4 - Indexing Strategy: "Account Statement" / "General Ledger" reports both filter by AccountId as their primary access path.
        builder.HasIndex(x => x.AccountId);
    }
}
