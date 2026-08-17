using ERP.Application.Common.Interfaces;
using ERP.Domain.Entities.Security;

namespace ERP.Security.Services;

/// <summary>
/// See <see cref="IAuditService"/>'s remarks for the persistence contract
/// each method follows - the split between self-persisting (authentication
/// events) and staged-only (entity/permission changes) is the important
/// design decision here, not the mechanics of building an
/// <see cref="AuditLogEntry"/> itself.
/// </summary>
public sealed class AuditService : IAuditService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _dateTime;

    public AuditService(IApplicationDbContext dbContext, ICurrentUserService currentUser, IDateTimeService dateTime)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _dateTime = dateTime;
    }

    public async Task LogAuthenticationEventAsync(Guid? userId, string userName, string action, string? reason = null, CancellationToken cancellationToken = default)
    {
        var entry = AuditLogEntry.ForAuthenticationEvent(userId, userName, _dateTime.NowUtc, action, reason);

        _dbContext.AuditLogEntries.Add(entry);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public void LogEntityChange(string action, string module, string affectedEntityType, Guid affectedEntityId, string? oldValuesJson = null, string? newValuesJson = null, string? reason = null, string? approvalStatus = null)
    {
        var currentUserId = RequireCurrentUserId();

        var entry = AuditLogEntry.ForEntityChange(
            currentUserId,
            _currentUser.UserName ?? "(unknown)",
            _currentUser.OfficeId,
            null, // Role snapshot: ICurrentUserService does not currently expose role names; left null rather than guessed. See milestone report - a small, well-contained follow-up if role-name context becomes available.
            _dateTime.NowUtc,
            action,
            module,
            affectedEntityType,
            affectedEntityId,
            oldValuesJson,
            newValuesJson,
            reason,
            approvalStatus);

        _dbContext.AuditLogEntries.Add(entry);
    }

    public void LogPermissionOrRoleChange(string action, string affectedEntityType, Guid affectedEntityId, string? oldValuesJson = null, string? newValuesJson = null, string? reason = null)
    {
        var currentUserId = RequireCurrentUserId();

        var entry = AuditLogEntry.ForPermissionOrRoleChange(
            currentUserId,
            _currentUser.UserName ?? "(unknown)",
            _dateTime.NowUtc,
            action,
            affectedEntityType,
            affectedEntityId,
            oldValuesJson,
            newValuesJson,
            reason);

        _dbContext.AuditLogEntries.Add(entry);
    }

    private Guid RequireCurrentUserId()
    {
        if (_currentUser.UserId is null)
        {
            throw new InvalidOperationException(
                "Cannot record an entity/permission audit entry with no authenticated current user. " +
                "System- or background-triggered changes should log under a well-known system user id instead of being silently unaudited.");
        }

        return _currentUser.UserId.Value;
    }
}
