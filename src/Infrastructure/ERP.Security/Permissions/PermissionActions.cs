namespace ERP.Security.Permissions;

/// <summary>
/// The fixed set of permission ACTIONS approved in Prompt 6's Permission
/// Engine ("Every permission must be independently assignable"). These are
/// combined with a per-module RESOURCE name (added as each module is
/// implemented, e.g. "Banks", "JournalEntries", "Contracts") to form a
/// concrete permission string such as <c>"Banks.Approve"</c> or
/// <c>"JournalEntries.Post"</c>.
///
/// Kept as plain string constants (not an enum) so that:
///   - new module resources can register new permission strings at
///     runtime/seed-time without a recompile of this shared list,
///   - permission strings serialize naturally into the Permission Matrix
///     stored in the database and into ASP.NET Core's policy-based
///     authorization (<see cref="Authorization.PermissionRequirement"/>).
/// </summary>
public static class PermissionActions
{
    public const string View = "View";
    public const string Create = "Create";
    public const string Edit = "Edit";
    public const string Delete = "Delete"; // logical/soft delete only - Prompt 4 & 6
    public const string Approve = "Approve";
    public const string Reject = "Reject";
    public const string Post = "Post";
    public const string Reverse = "Reverse";
    public const string Print = "Print";
    public const string Export = "Export";
    public const string Import = "Import";
    public const string ClosePeriod = "ClosePeriod";
    public const string ReopenPeriod = "ReopenPeriod";
    public const string Administration = "Administration";
    public const string Configuration = "Configuration";

    public static readonly IReadOnlyList<string> All = new[]
    {
        View, Create, Edit, Delete, Approve, Reject, Post, Reverse,
        Print, Export, Import, ClosePeriod, ReopenPeriod, Administration, Configuration
    };

    /// <summary>Builds the concrete "{Resource}.{Action}" permission string used throughout the Permission Matrix (Prompt 6 deliverable) and policy-based authorization.</summary>
    public static string For(string resource, string action) => $"{resource}.{action}";
}

/// <summary>
/// Built-in system permission resources that exist independently of any
/// single business module (as opposed to future per-module resources like
/// "Banks" or "Contracts", registered when those modules are implemented).
/// </summary>
public static class SystemPermissionResources
{
    public const string Users = "Users";
    public const string Roles = "Roles";
    public const string Permissions = "Permissions";
    public const string SystemConfiguration = "SystemConfiguration";
    public const string AuditLog = "AuditLog";
}
