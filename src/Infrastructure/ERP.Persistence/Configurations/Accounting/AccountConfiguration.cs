using ERP.Domain.Entities.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Persistence.Configurations.Accounting;

/// <summary>
/// Prompt 4 / Prompt 5 - Chart of Accounts. Self-referencing one-to-many
/// (ParentAccountId -> Id) modeling the account hierarchy described in
/// Prompt 5's Chart of Accounts Design (Parent/Child/Posting/Control
/// accounts, account levels).
/// </summary>
public sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("Accounts", schema: "accounting");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code)
            .HasMaxLength(20)
            .IsRequired();

        // Prompt 4 - Unique Constraints: account codes are unique system-wide, not just per-branch, so a code unambiguously identifies one account anywhere in the Chart of Accounts.
        builder.HasIndex(x => x.Code)
            .IsUnique();

        builder.Property(x => x.NameAr).HasMaxLength(150).IsRequired();
        builder.Property(x => x.NameEn).HasMaxLength(150).IsRequired();

        builder.Property(x => x.AccountType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.NormalBalance)
            .HasConversion<string>()
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(x => x.Classification)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Level).IsRequired();

        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.IsSystemReserved).IsRequired();

        // Self-referencing hierarchy: a child's ParentAccountId points back at this same table's Id.
        builder.HasOne<Account>()
            .WithMany(x => x.Children)
            .HasForeignKey(x => x.ParentAccountId)
            .OnDelete(DeleteBehavior.Restrict); // Prompt 4: never cascade-delete an entire account sub-tree by accident.

        builder.Navigation(x => x.Children)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Prompt 4 - Indexing Strategy: reporting/filtering by type and by active-status are both extremely common (Chart of Accounts screen, every financial report's account filter).
        builder.HasIndex(x => x.AccountType);
        builder.HasIndex(x => x.ParentAccountId);
        builder.HasIndex(x => x.IsActive);
    }
}
