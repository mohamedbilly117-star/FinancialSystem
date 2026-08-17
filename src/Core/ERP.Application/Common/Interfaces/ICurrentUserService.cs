namespace ERP.Application.Common.Interfaces;

/// <summary>
/// Who is making the current request. Implemented in ERP.Infrastructure by
/// reading the authenticated Blazor Server circuit's ClaimsPrincipal.
/// Every use case that writes audit fields, evaluates office/department
/// data isolation (Prompt 6), or records "Created By" on a new entity goes
/// through this abstraction rather than touching HttpContext directly -
/// keeping ERP.Application free of any ASP.NET Core package dependency.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>Id of the authenticated ApplicationUser, or null for unauthenticated/system contexts (e.g. seeding, background jobs).</summary>
    Guid? UserId { get; }

    /// <summary>Display name / username, for audit log readability without an extra join.</summary>
    string? UserName { get; }

    /// <summary>
    /// The Office the current user is acting within (Bank Office / Accounting
    /// Office / Incentives Office / future offices - Prompt 1 &amp; Prompt 6).
    /// Drives Office Isolation data-security rules.
    /// </summary>
    Guid? OfficeId { get; }

    /// <summary>Department the current user belongs to, for Department Isolation (Prompt 6).</summary>
    Guid? DepartmentId { get; }

    bool IsAuthenticated { get; }
}

/// <summary>
/// Testable clock abstraction. The ERP is offline/LAN-deployed, so this
/// always returns server time (no timezone-conversion concerns across
/// sites), but abstracting it keeps unit tests deterministic and keeps
/// "now" out of every module's hands-on DateTime.UtcNow calls, which
/// matters heavily for period-closing and fiscal-year logic (Prompt 10)
/// that must be exercised with fixed, repeatable dates in tests.
/// </summary>
public interface IDateTimeService
{
    DateTime NowUtc { get; }

    DateOnly TodayLocal { get; }
}

/// <summary>
/// Evaluates the granular Permission Engine defined in Prompt 6 (View /
/// Create / Edit / Delete / Approve / Reject / Post / Reverse / Print /
/// Export / Import / ClosePeriod / ReopenPeriod / Administration /
/// Configuration). Implemented in ERP.Security. Application-layer use
/// cases call <c>AuthorizeAsync</c> before executing any protected
/// operation rather than re-implementing permission checks per module,
/// satisfying Prompt 6's "authorization rules must never be duplicated".
/// </summary>
public interface IPermissionService
{
    Task<bool> HasPermissionAsync(Guid userId, string permission, CancellationToken cancellationToken = default);

    Task AuthorizeAsync(Guid userId, string permission, CancellationToken cancellationToken = default);
}

/// <summary>
/// Prompt 6 - Audit Framework: "Every important action must be recorded."
/// The write side of <c>AuditLogEntry</c> - implemented in ERP.Security.
///
/// <see cref="LogAuthenticationEventAsync"/> persists immediately (calls
/// <c>IApplicationDbContext.SaveChangesAsync</c> itself) because a Login/
/// Logout/Failed Login event is standalone - it has no other business
/// unit-of-work to ride along with.
///
/// <see cref="LogEntityChange"/> and <see cref="LogPermissionOrRoleChange"/>
/// are deliberately synchronous and do NOT call SaveChangesAsync - they
/// only stage the entry on the current change tracker. The calling
/// Application-layer use case is expected to call them immediately before
/// its own single outer <c>SaveChangesAsync</c>, so the audit record and
/// the business change it describes (e.g. a JournalEntry's Post) commit
/// together in the exact same database transaction and can never end up
/// out of sync with each other.
/// </summary>
public interface IAuditService
{
    Task LogAuthenticationEventAsync(Guid? userId, string userName, string action, string? reason = null, CancellationToken cancellationToken = default);

    /// <summary>Uses the current authenticated user (<see cref="ICurrentUserService"/>) as the actor - throws <see cref="InvalidOperationException"/> if called with no authenticated user, since a system/background-triggered change should log under a well-known system user id rather than silently go unaudited.</summary>
    void LogEntityChange(string action, string module, string affectedEntityType, Guid affectedEntityId, string? oldValuesJson = null, string? newValuesJson = null, string? reason = null, string? approvalStatus = null);

    void LogPermissionOrRoleChange(string action, string affectedEntityType, Guid affectedEntityId, string? oldValuesJson = null, string? newValuesJson = null, string? reason = null);
}

/// <summary>
/// Prompt 4 / Prompt 11 - Numbering Configuration, implemented against
/// <c>ERP.Domain.Entities.Configuration.NumberingSequence</c>. Implemented
/// in ERP.Infrastructure (a general cross-cutting concern, not Identity-
/// specific like <see cref="IPermissionService"/>/<see cref="IAuditService"/>
/// in ERP.Security).
/// </summary>
public interface INumberingSequenceService
{
    /// <summary>
    /// Atomically advances and returns the next formatted number for
    /// <paramref name="sequenceKey"/> (see
    /// <c>ERP.Shared.Constants.NumberingSequenceKeys</c> for the standard
    /// keys), retrying on a detected optimistic-concurrency conflict up to
    /// a fixed maximum before giving up.
    /// </summary>
    /// <param name="sequenceKey">Which sequence to advance, e.g. "JOURNAL".</param>
    /// <param name="fiscalYearId">Required when the sequence's configured ResetPolicy is Yearly - selects which fiscal year's row to advance; must be null otherwise.</param>
    /// <exception cref="InvalidOperationException">No active sequence is configured for the given key/scope, or the maximum retry count was exceeded due to sustained concurrent contention.</exception>
    Task<string> GenerateNextAsync(string sequenceKey, Guid? fiscalYearId = null, CancellationToken cancellationToken = default);
}
