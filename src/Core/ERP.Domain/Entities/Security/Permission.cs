using ERP.Domain.Common;
using ERP.Shared.Guards;

namespace ERP.Domain.Entities.Security;

/// <summary>
/// Prompt 6 - Permission Engine: "Design a granular permission engine...
/// Every permission must be independently assignable." One row in the
/// system-wide catalog of possible permissions - e.g. Module="Accounting",
/// Resource="JournalEntry", Action="Post", Code="JournalEntry.Post".
///
/// <see cref="Code"/> MUST stay in the exact "{Resource}.{Action}" format
/// produced by <c>ERP.Security.Permissions.PermissionActions.For</c> -
/// that string is what <c>[Authorize(Policy = "...")]</c> attributes and
/// <c>IPermissionService.HasPermissionAsync</c> compare against. This
/// entity cannot reference that helper directly (ERP.Domain must never
/// depend on ERP.Security - Prompt 3's layering rule), so the format is
/// duplicated here as a plain string interpolation; the two are kept in
/// sync by test coverage on both sides rather than a shared reference.
///
/// Seeded/system-defined (the software itself determines which
/// permissions CAN exist, per Prompt 6's fixed action vocabulary), while
/// WHICH roles hold which permissions is fully administrator-configurable
/// via <see cref="RolePermission"/> - the same seeded-catalog/configurable-
/// assignment split already used for
/// <see cref="Accounting.RuleEngine.AccountingRule"/>'s fixed Debit/Credit
/// taxonomy vs. its configurable rule assignments.
/// </summary>
public sealed class Permission : AuditableEntity, IAggregateRoot, ISoftDelete
{
    public string Code { get; private set; } = string.Empty;

    /// <summary>Top-level grouping for the Permission Matrix admin screen (Prompt 6's "Screen Security": Modules) - e.g. "Accounting", "Security".</summary>
    public string Module { get; private set; } = string.Empty;

    /// <summary>The screen/feature within the module - e.g. "JournalEntry", "FiscalYear", "Users".</summary>
    public string Resource { get; private set; } = string.Empty;

    /// <summary>One of <c>ERP.Security.Permissions.PermissionActions</c>'s fixed verbs (View/Create/Edit/Delete/Approve/Reject/Post/Reverse/Print/Export/Import/ClosePeriod/ReopenPeriod/Administration/Configuration).</summary>
    public string Action { get; private set; } = string.Empty;

    public string NameAr { get; private set; } = string.Empty;

    public string NameEn { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    /// <summary>True for permissions the software itself defines (virtually all of them) - administrators assign/revoke them to roles freely but never delete the permission definition itself, mirroring <c>Account.IsSystemReserved</c>.</summary>
    public bool IsSystemPermission { get; private set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAtUtc { get; set; }

    public Guid? DeletedBy { get; set; }

    private Permission()
    {
        // Required by EF Core.
    }

    private Permission(Guid id, string module, string resource, string action, string nameAr, string nameEn, string? description, bool isSystemPermission)
    {
        Id = id;
        Module = module;
        Resource = resource;
        Action = action;
        Code = $"{resource}.{action}";
        NameAr = nameAr;
        NameEn = nameEn;
        Description = description;
        IsSystemPermission = isSystemPermission;
    }

    public static Permission Create(string module, string resource, string action, string nameAr, string nameEn, string? description = null, bool isSystemPermission = true)
    {
        Guard.AgainstNullOrWhiteSpace(module, nameof(module));
        Guard.AgainstNullOrWhiteSpace(resource, nameof(resource));
        Guard.AgainstNullOrWhiteSpace(action, nameof(action));
        Guard.AgainstNullOrWhiteSpace(nameAr, nameof(nameAr));
        Guard.AgainstNullOrWhiteSpace(nameEn, nameof(nameEn));

        return new Permission(Guid.NewGuid(), module, resource, action, nameAr, nameEn, description, isSystemPermission);
    }
}

/// <summary>
/// Prompt 6 - "Every permission must be independently assignable." The
/// actual Role &lt;-&gt; Permission Matrix cell: one grant of one
/// <see cref="Permission"/> to one role.
///
/// <see cref="RoleId"/> is a bare <see cref="Guid"/>, not a typed
/// reference to <c>ApplicationRole</c> - the same forward-reference
/// pattern already used by <c>JournalEntry.SourceReferenceId</c> and
/// <c>DistributionTemplate.SourceReferenceId</c>, needed here because
/// <c>ApplicationRole</c> lives in ERP.Security, which ERP.Domain must
/// never reference. The real foreign-key constraint down to
/// <c>ApplicationRole</c> is added in
/// <c>ERP.Persistence.Configurations.Security.RolePermissionConfiguration</c>,
/// which - unlike ERP.Domain - does have visibility into both sides.
///
/// Implements <see cref="ISoftDelete"/> rather than being hard-deleted
/// when a permission is revoked, satisfying Prompt 6's Audit Framework:
/// "Permission Changes" must remain reconstructable - which permissions a
/// role held, and for how long, is itself an audit-relevant fact.
/// </summary>
public sealed class RolePermission : AuditableEntity, ISoftDelete
{
    public Guid RoleId { get; private set; }

    public Guid PermissionId { get; private set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAtUtc { get; set; }

    public Guid? DeletedBy { get; set; }

    private RolePermission()
    {
        // Required by EF Core.
    }

    private RolePermission(Guid id, Guid roleId, Guid permissionId)
    {
        Id = id;
        RoleId = roleId;
        PermissionId = permissionId;
    }

    public static RolePermission Create(Guid roleId, Guid permissionId)
    {
        Guard.AgainstEmpty(roleId, nameof(roleId));
        Guard.AgainstEmpty(permissionId, nameof(permissionId));

        return new RolePermission(Guid.NewGuid(), roleId, permissionId);
    }
}
