using ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Persistence.Configurations.Security;

/// <summary>
/// Prompt 6 - Audit Framework. No FK constraint to Users/Roles/business
/// entities is added here deliberately: an audit record must remain
/// readable even after the user, role, or affected entity it references
/// is later deleted (per Prompt 6, users/entities are only ever logically
/// deleted, but an audit log's job is specifically to survive that kind
/// of change without itself being affected) - UserName/RoleNamesSnapshot
/// are denormalized text precisely so the log never depends on a live
/// join succeeding.
/// </summary>
public sealed class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> builder)
    {
        builder.ToTable("AuditLogEntries", schema: "security");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId);
        builder.Property(x => x.UserName).HasMaxLength(256);
        builder.Property(x => x.OfficeId);
        builder.Property(x => x.RoleNamesSnapshot).HasMaxLength(500);

        builder.Property(x => x.OccurredAtUtc).IsRequired();

        builder.Property(x => x.Action).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Module).HasMaxLength(50);
        builder.Property(x => x.AffectedEntityType).HasMaxLength(100);
        builder.Property(x => x.AffectedEntityId);

        // Old/New value snapshots: unbounded text, since a full entity
        // diff's serialized size is not predictable in advance.
        builder.Property(x => x.OldValuesJson).HasColumnType("nvarchar(max)");
        builder.Property(x => x.NewValuesJson).HasColumnType("nvarchar(max)");

        builder.Property(x => x.Reason).HasMaxLength(1000);
        builder.Property(x => x.ApprovalStatus).HasMaxLength(30);
        builder.Property(x => x.SessionId).HasMaxLength(100);

        // Prompt 6 - Audit Framework / Prompt 9 - Report Filters: every
        // audit/compliance query filters primarily by who, what happened,
        // when, and against which record.
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.Action);
        builder.HasIndex(x => x.OccurredAtUtc);
        builder.HasIndex(x => new { x.AffectedEntityType, x.AffectedEntityId });
    }
}
