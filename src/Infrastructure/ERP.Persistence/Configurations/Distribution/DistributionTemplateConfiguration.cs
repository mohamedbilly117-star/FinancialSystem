using ERP.Domain.Entities.Accounting.Distribution;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Persistence.Configurations.Distribution;

/// <summary>
/// Prompt 11 addendum - DistributionTemplate. The unique filtered index on
/// (SourceType, SourceReferenceId) WHERE IsActive = 1 is what actually
/// makes "automatic selection of the correct template during transaction
/// processing" unambiguous: the database itself refuses to let two
/// templates for the same source instance be Active at once, so a future
/// lookup query can never find more than one candidate.
/// </summary>
public sealed class DistributionTemplateConfiguration : IEntityTypeConfiguration<DistributionTemplate>
{
    public void Configure(EntityTypeBuilder<DistributionTemplate> builder)
    {
        builder.ToTable("DistributionTemplates", schema: "accounting");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.SourceType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.SourceReferenceId).IsRequired();

        builder.Property(x => x.Code).HasMaxLength(30).IsRequired();
        builder.Property(x => x.NameAr).HasMaxLength(150).IsRequired();
        builder.Property(x => x.NameEn).HasMaxLength(150).IsRequired();

        builder.Property(x => x.Method)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Version).IsRequired();

        builder.Property(x => x.EffectiveFrom).HasColumnType("date").IsRequired();
        builder.Property(x => x.EffectiveTo).HasColumnType("date");

        builder.Property(x => x.IsActive).IsRequired();

        builder.Property(x => x.IsDeleted).IsRequired();
        builder.Property(x => x.DeletedAtUtc);

        // Prompt 11 addendum: "No global distribution percentages may be
        // assumed" - enforced here as "at most one Active template per
        // source instance", never a fallback/default row.
        builder.HasIndex(x => new { x.SourceType, x.SourceReferenceId, x.IsActive })
            .IsUnique()
            .HasFilter("[IsActive] = 1")
            .HasDatabaseName("IX_DistributionTemplates_ActiveSource");

        // Every version of the same source's template lineage, in order.
        builder.HasIndex(x => new { x.SourceType, x.SourceReferenceId, x.Version });

        builder.HasMany(x => x.Lines)
            .WithOne()
            .HasForeignKey(l => l.DistributionTemplateId)
            .OnDelete(DeleteBehavior.Cascade); // Lines have no meaning without their template and are only ever created/removed through it - same reasoning as JournalEntryLine.

        builder.Navigation(x => x.Lines)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

/// <summary>
/// Prompt 11 addendum - DistributionTemplateLine. Percentage/FixedAmount
/// both use decimal(18,4), matching JournalEntryLine's money precision for
/// consistency across the Accounting Engine.
/// </summary>
public sealed class DistributionTemplateLineConfiguration : IEntityTypeConfiguration<DistributionTemplateLine>
{
    public void Configure(EntityTypeBuilder<DistributionTemplateLine> builder)
    {
        builder.ToTable("DistributionTemplateLines", schema: "accounting");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.DistributionTemplateId).IsRequired();
        builder.Property(x => x.LineNumber).IsRequired();

        builder.HasIndex(x => new { x.DistributionTemplateId, x.LineNumber }).IsUnique();

        builder.Property(x => x.DestinationAccountId).IsRequired();

        builder.Property(x => x.Percentage).HasPrecision(18, 4);
        builder.Property(x => x.FixedAmount).HasPrecision(18, 4);

        builder.Property(x => x.DescriptionAr).HasMaxLength(500);
        builder.Property(x => x.DescriptionEn).HasMaxLength(500);

        builder.HasOne<ERP.Domain.Entities.Accounting.Account>()
            .WithMany()
            .HasForeignKey(x => x.DestinationAccountId)
            .OnDelete(DeleteBehavior.Restrict); // Same reasoning as JournalEntryLine -> Account: never cascade-delete an account referenced by a distribution rule.

        builder.HasIndex(x => x.DestinationAccountId);
    }
}
