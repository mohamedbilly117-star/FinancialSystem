using ERP.Domain.Entities.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Persistence.Configurations.Configuration;

/// <summary>
/// Prompt 4 / Prompt 11 - Numbering Configuration. The unique index on
/// (SequenceKey, FiscalYearId) is what makes
/// <c>NumberingSequenceService</c>'s lookup unambiguous - at most one
/// sequence row exists per key per fiscal-year scope (a NULL
/// FiscalYearId, i.e. a Never-reset sequence, is naturally still unique
/// under standard SQL Server unique-index NULL semantics, matching the
/// same pattern already used for JournalNumber elsewhere).
/// </summary>
public sealed class NumberingSequenceConfiguration : IEntityTypeConfiguration<NumberingSequence>
{
    public void Configure(EntityTypeBuilder<NumberingSequence> builder)
    {
        builder.ToTable("NumberingSequences", schema: "configuration");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.SequenceKey).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Prefix).HasMaxLength(NumberingSequence.MaxPrefixLength).IsRequired();
        builder.Property(x => x.PaddingLength).IsRequired();
        builder.Property(x => x.CurrentValue).IsRequired();

        builder.Property(x => x.ResetPolicy).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.Property(x => x.FiscalYearId);
        builder.Property(x => x.IsActive).IsRequired();

        builder.HasIndex(x => new { x.SequenceKey, x.FiscalYearId }).IsUnique();
    }
}

public sealed class SystemSettingConfiguration : IEntityTypeConfiguration<SystemSetting>
{
    public void Configure(EntityTypeBuilder<SystemSetting> builder)
    {
        builder.ToTable("SystemSettings", schema: "configuration");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Key).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => x.Key).IsUnique();

        builder.Property(x => x.Value).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(x => x.Category).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500);

        builder.HasIndex(x => x.Category);
    }
}
