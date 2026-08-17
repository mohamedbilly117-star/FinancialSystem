using ERP.Domain.Common;
using ERP.Shared.Guards;

namespace ERP.Domain.Entities.Security;

/// <summary>
/// Prompt 6 - Audit Framework: "Every important action must be recorded:
/// Login, Logout, Failed Login, Password Change, Create Record, Modify
/// Record, Delete Record (Logical), Approve, Reject, Post, Reverse, Print,
/// Export, Configuration Changes, Permission Changes, Role Changes,
/// Workflow Actions." And Audit Details: "User, Office, Role, ... Session
/// Identifier, Date, Time, Action, Affected Module, Affected Record, Old
/// Values, New Values, Reason, Approval Status."
///
/// Distinct from <see cref="AuditableEntity"/>'s CreatedBy/ModifiedBy
/// fields (which every business entity already carries automatically via
/// <c>AuditableEntitySaveChangesInterceptor</c>): those record CRUD
/// metadata on the entity itself, but cannot represent events with no
/// entity at all (Login, Logout, Failed Login, a denied authorization
/// attempt) or a full old/new value diff. This entity is the general-
/// purpose event log those per-entity fields cannot be.
///
/// IP Address and Machine Name are Prompt 6's own explicitly-marked
/// "(future)" fields - intentionally not modeled here yet, kept out
/// rather than added as permanently-null placeholders.
/// </summary>
public sealed class AuditLogEntry : BaseEntity
{
    /// <summary>Null for events where no authenticated user exists yet - e.g. a Failed Login attempt against a username that may not even correspond to a real account.</summary>
    public Guid? UserId { get; private set; }

    /// <summary>Denormalized username snapshot - kept even if the user account is later renamed or deleted, since the audit record must reflect what was true at the time.</summary>
    public string? UserName { get; private set; }

    public Guid? OfficeId { get; private set; }

    /// <summary>Comma-separated role names snapshot at the time of the action (Prompt 6 Audit Details: "Role") - denormalized for the same reason as <see cref="UserName"/>: a role's permissions (or the role itself) can change later without altering what the audit record says applied at the time.</summary>
    public string? RoleNamesSnapshot { get; private set; }

    public DateTime OccurredAtUtc { get; private set; }

    /// <summary>E.g. "Login", "Logout", "FailedLogin", "PasswordChange", or one of <c>ERP.Security.Permissions.PermissionActions</c>'s verbs (Create/Edit/Approve/Post/Reverse/...). Free-form by design - the fixed vocabulary lives in ERP.Security (see <see cref="Permission"/>'s remarks on why ERP.Domain cannot reference it directly), not enforced here.</summary>
    public string Action { get; private set; } = string.Empty;

    public string? Module { get; private set; }

    public string? AffectedEntityType { get; private set; }

    public Guid? AffectedEntityId { get; private set; }

    /// <summary>JSON snapshot of the affected fields before the change, or null for events with no meaningful "before" state (e.g. Login).</summary>
    public string? OldValuesJson { get; private set; }

    public string? NewValuesJson { get; private set; }

    /// <summary>Free-text justification - required by the calling Application-layer use case for sensitive actions (e.g. a manual journal reversal), optional for routine ones.</summary>
    public string? Reason { get; private set; }

    /// <summary>Only meaningful for approval-workflow actions (Prompt 6 Audit Details: "Approval Status") - e.g. "Approved", "Rejected". Null for non-approval events.</summary>
    public string? ApprovalStatus { get; private set; }

    /// <summary>Future-ready (Prompt 6: "Session Identifier") - populated once Blazor Server circuit/session correlation is wired up in a later milestone; nullable and unused until then.</summary>
    public string? SessionId { get; private set; }

    private AuditLogEntry()
    {
        // Required by EF Core.
    }

    private AuditLogEntry(
        Guid id,
        Guid? userId,
        string? userName,
        Guid? officeId,
        string? roleNamesSnapshot,
        DateTime occurredAtUtc,
        string action,
        string? module,
        string? affectedEntityType,
        Guid? affectedEntityId,
        string? oldValuesJson,
        string? newValuesJson,
        string? reason,
        string? approvalStatus,
        string? sessionId)
    {
        Id = id;
        UserId = userId;
        UserName = userName;
        OfficeId = officeId;
        RoleNamesSnapshot = roleNamesSnapshot;
        OccurredAtUtc = occurredAtUtc;
        Action = action;
        Module = module;
        AffectedEntityType = affectedEntityType;
        AffectedEntityId = affectedEntityId;
        OldValuesJson = oldValuesJson;
        NewValuesJson = newValuesJson;
        Reason = reason;
        ApprovalStatus = approvalStatus;
        SessionId = sessionId;
    }

    /// <summary>General-purpose factory - the other three below are ergonomic shortcuts for the three most common audit scenarios and all ultimately call this one.</summary>
    public static AuditLogEntry Create(
        Guid? userId,
        string? userName,
        Guid? officeId,
        string? roleNamesSnapshot,
        DateTime occurredAtUtc,
        string action,
        string? module = null,
        string? affectedEntityType = null,
        Guid? affectedEntityId = null,
        string? oldValuesJson = null,
        string? newValuesJson = null,
        string? reason = null,
        string? approvalStatus = null,
        string? sessionId = null)
    {
        Guard.AgainstNullOrWhiteSpace(action, nameof(action));

        return new AuditLogEntry(
            Guid.NewGuid(),
            userId,
            userName,
            officeId,
            roleNamesSnapshot,
            occurredAtUtc,
            action,
            module,
            affectedEntityType,
            affectedEntityId,
            oldValuesJson,
            newValuesJson,
            reason,
            approvalStatus,
            sessionId);
    }

    /// <summary>Prompt 6: "Login. Logout. Failed Login. Password Change." No entity, module, or approval status is meaningful for these.</summary>
    public static AuditLogEntry ForAuthenticationEvent(Guid? userId, string userName, DateTime occurredAtUtc, string action, string? reason = null)
    {
        Guard.AgainstNullOrWhiteSpace(userName, nameof(userName));

        return Create(userId, userName, null, null, occurredAtUtc, action, null, null, null, null, null, reason, null, null);
    }

    /// <summary>Prompt 6: "Create Record. Modify Record. Delete Record. Approve. Reject. Post. Reverse." - a change to (or action against) one specific business entity.</summary>
    public static AuditLogEntry ForEntityChange(
        Guid userId,
        string userName,
        Guid? officeId,
        string? roleNamesSnapshot,
        DateTime occurredAtUtc,
        string action,
        string module,
        string affectedEntityType,
        Guid affectedEntityId,
        string? oldValuesJson = null,
        string? newValuesJson = null,
        string? reason = null,
        string? approvalStatus = null)
    {
        Guard.AgainstEmpty(userId, nameof(userId));
        Guard.AgainstNullOrWhiteSpace(userName, nameof(userName));
        Guard.AgainstNullOrWhiteSpace(module, nameof(module));
        Guard.AgainstNullOrWhiteSpace(affectedEntityType, nameof(affectedEntityType));
        Guard.AgainstEmpty(affectedEntityId, nameof(affectedEntityId));

        return Create(userId, userName, officeId, roleNamesSnapshot, occurredAtUtc, action, module, affectedEntityType, affectedEntityId, oldValuesJson, newValuesJson, reason, approvalStatus, null);
    }

    /// <summary>Prompt 6: "Permission Changes. Role Changes." Always Module="Security"; no OfficeId/role-snapshot dimension, since these events are ABOUT roles/permissions themselves.</summary>
    public static AuditLogEntry ForPermissionOrRoleChange(
        Guid userId,
        string userName,
        DateTime occurredAtUtc,
        string action,
        string affectedEntityType,
        Guid affectedEntityId,
        string? oldValuesJson = null,
        string? newValuesJson = null,
        string? reason = null)
    {
        Guard.AgainstEmpty(userId, nameof(userId));
        Guard.AgainstNullOrWhiteSpace(userName, nameof(userName));
        Guard.AgainstNullOrWhiteSpace(affectedEntityType, nameof(affectedEntityType));
        Guard.AgainstEmpty(affectedEntityId, nameof(affectedEntityId));

        return Create(userId, userName, null, null, occurredAtUtc, action, "Security", affectedEntityType, affectedEntityId, oldValuesJson, newValuesJson, reason, null, null);
    }
}
