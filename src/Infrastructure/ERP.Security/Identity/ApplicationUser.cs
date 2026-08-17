using Microsoft.AspNetCore.Identity;

namespace ERP.Security.Identity;

/// <summary>
/// ASP.NET Core Identity user, extended with the organizational context
/// required by Prompt 6's Organizational Access Model and Data Security
/// (Office Isolation / Department Isolation) and Prompt 1's discovered
/// stakeholder model (Director, Department Manager, Section Head, Bank/
/// Accounting/Incentives Office staff, Auditor, System Administrator).
///
/// Uses a <see cref="Guid"/> key (rather than the default string/int) to
/// stay consistent with <c>BaseEntity</c>'s Guid primary keys across the
/// rest of the Domain model, and because CreatedBy/ModifiedBy/ApprovedBy
/// audit columns throughout the approved Database Design (Prompt 4) are
/// typed as Guid foreign keys into the Users table.
///
/// Full account-lifecycle fields (Disabled, Locked, Password Expiration,
/// Password History, Forced Password Change) map onto ASP.NET Core
/// Identity's built-in LockoutEnabled/LockoutEnd/AccessFailedCount plus a
/// small number of custom fields below - the *policies* that drive them
/// (max attempts, expiration days, etc.) are configurable data (Prompt 11),
/// implemented in the Security phase per the Prompt 13 roadmap, not
/// hardcoded here.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    /// <summary>Full display name (Arabic first, per Prompt 8) shown in audit logs, notifications and the UI header/profile.</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>The Office this user primarily belongs to (Bank Office / Accounting Office / Incentives Office / future offices) - drives Office Isolation (Prompt 6).</summary>
    public Guid? OfficeId { get; set; }

    /// <summary>Department for Department Isolation (Prompt 6).</summary>
    public Guid? DepartmentId { get; set; }

    /// <summary>True for a user account created as disabled (Prompt 6: "Disabled Users") - distinct from Identity's LockoutEnd, which represents a temporary/automatic lock rather than an administrator's deliberate disable action.</summary>
    public bool IsDisabled { get; set; }

    /// <summary>Forces a password change on next login (Prompt 6: "Forced Password Change") - e.g. after an administrator reset.</summary>
    public bool MustChangePasswordOnNextLogin { get; set; }

    public DateTime? PasswordLastChangedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public Guid? CreatedBy { get; set; }
}

/// <summary>
/// ASP.NET Core Identity role, backing the Role Management design in
/// Prompt 6 (Built-in Roles, Custom Roles, Role Templates, Role
/// Categories). Actual role instances (System Administrator, Director,
/// Department Manager, Section Head, Bank Office, Accounting Office,
/// Incentives Office, Auditor, ...) are seeded/configured data, not
/// hardcoded types, so administrators can add future roles without a
/// code change.
/// </summary>
public class ApplicationRole : IdentityRole<Guid>
{
    /// <summary>Optional free-text description shown in the Role Management admin screen.</summary>
    public string? Description { get; set; }

    /// <summary>True for the small set of roles the system itself depends on (e.g. System Administrator) that administrators may edit permissions for, but never delete - Prompt 6: "Built-in Roles".</summary>
    public bool IsSystemRole { get; set; }
}
