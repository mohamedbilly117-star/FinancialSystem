using ERP.Domain.Entities.Security;
using ERP.Security.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Persistence.Configurations.Security;

/// <summary>
/// Prompt 6 - Permission Engine. The system-wide permission catalog.
/// </summary>
public sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("Permissions", schema: "security");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).HasMaxLength(150).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();

        builder.Property(x => x.Module).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Resource).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Action).HasMaxLength(30).IsRequired();

        builder.Property(x => x.NameAr).HasMaxLength(150).IsRequired();
        builder.Property(x => x.NameEn).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500);

        builder.Property(x => x.IsSystemPermission).IsRequired();

        builder.Property(x => x.IsDeleted).IsRequired();
        builder.Property(x => x.DeletedAtUtc);

        builder.HasIndex(x => new { x.Module, x.Resource });
    }
}

/// <summary>
/// Prompt 6 - Role-to-permission assignment (the Permission Matrix's
/// actual cells). This is the one place in the whole solution where an
/// ERP.Domain entity (<see cref="RolePermission"/>, whose own
/// <c>RoleId</c> property is deliberately just a bare <see cref="Guid"/>
/// to keep Domain free of an ERP.Security reference) gets its real
/// foreign-key constraint added - ERP.Persistence references both
/// ERP.Domain and ERP.Security, so it is the only layer able to see both
/// sides of this relationship at once.
/// </summary>
public sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("RolePermissions", schema: "security");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.RoleId).IsRequired();
        builder.Property(x => x.PermissionId).IsRequired();

        builder.Property(x => x.IsDeleted).IsRequired();
        builder.Property(x => x.DeletedAtUtc);

        // A given Role/Permission pair should only ever have one CURRENT
        // (non-deleted) assignment row - re-granting a previously-revoked
        // permission creates a new row rather than resurrecting the old
        // one, preserving the full grant/revoke history (Prompt 6:
        // "Permission Changes" must remain auditable).
        builder.HasIndex(x => new { x.RoleId, x.PermissionId, x.IsDeleted })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("IX_RolePermissions_ActiveGrant");

        builder.HasOne<Permission>()
            .WithMany()
            .HasForeignKey(x => x.PermissionId)
            .OnDelete(DeleteBehavior.Restrict);

        // The real FK down to ApplicationRole - see class remarks.
        builder.HasOne<ApplicationRole>()
            .WithMany()
            .HasForeignKey(x => x.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.RoleId);
    }
}
