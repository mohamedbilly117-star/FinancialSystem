using ERP.Application.Common.Interfaces;
using ERP.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ERP.Persistence.Interceptors;

/// <summary>
/// Implements, in exactly one place, two mandatory rules that must never be
/// left to individual module developers to remember:
///
/// 1. Prompt 4 / Prompt 6 audit fields: every <see cref="IAuditableEntity"/>
///    automatically gets CreatedBy/CreatedAtUtc set on insert, and
///    ModifiedBy/ModifiedAtUtc set on update - populated from
///    <see cref="ICurrentUserService"/>, never trusted from client input.
///
/// 2. Prompt 4's rule that "no financial transaction may be physically
///    deleted": any tracked entity implementing <see cref="ISoftDelete"/>
///    that the application code calls <c>Remove()</c> on on is
///    transparently converted into an UPDATE (IsDeleted = true,
///    DeletedAtUtc, DeletedBy) instead of a DELETE, so a module can never
///    accidentally bypass the soft-delete requirement by calling the
///    ordinary EF Core Remove API.
/// </summary>
public sealed class AuditableEntitySaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeService _dateTimeService;

    public AuditableEntitySaveChangesInterceptor(
        ICurrentUserService currentUserService,
        IDateTimeService dateTimeService)
    {
        _currentUserService = currentUserService;
        _dateTimeService = dateTimeService;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        UpdateAuditableEntities(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        UpdateAuditableEntities(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void UpdateAuditableEntities(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var userId = _currentUserService.UserId ?? Guid.Empty;
        var nowUtc = _dateTimeService.NowUtc;

        foreach (var entry in context.ChangeTracker.Entries<ISoftDelete>())
        {
            // Rule 2: convert a hard Delete into a soft delete, unconditionally.
            if (entry.State == EntityState.Deleted)
            {
                entry.State = EntityState.Modified;
                entry.Entity.IsDeleted = true;
                entry.Entity.DeletedAtUtc = nowUtc;
                entry.Entity.DeletedBy = userId;
            }
        }

        foreach (var entry in context.ChangeTracker.Entries<IAuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedBy = userId;
                    entry.Entity.CreatedAtUtc = nowUtc;
                    break;

                case EntityState.Modified:
                    entry.Entity.ModifiedBy = userId;
                    entry.Entity.ModifiedAtUtc = nowUtc;
                    // Created* fields must be immutable after insert - defend
                    // against a module accidentally re-sending them on update.
                    entry.Property(nameof(IAuditableEntity.CreatedBy)).IsModified = false;
                    entry.Property(nameof(IAuditableEntity.CreatedAtUtc)).IsModified = false;
                    break;
            }
        }

        DispatchDomainEventsPostSaveMarker(context);
    }

    /// <summary>
    /// Domain events collected on entities implementing
    /// <see cref="IHasDomainEvents"/> are intentionally NOT dispatched here
    /// (SavingChanges runs BEFORE the transaction commits). Actual
    /// dispatch - e.g. handing a "RevenueRecordedEvent" to the Accounting
    /// Engine to generate a balanced journal entry per Prompt 5 - happens
    /// from <c>ApplicationDbContext.SaveChangesAsync</c> AFTER
    /// <c>base.SaveChangesAsync</c> returns successfully, implemented when
    /// the first module that raises domain events is built. This method is
    /// left as an explicit extension point/reminder rather than silently
    /// omitted.
    /// </summary>
    private static void DispatchDomainEventsPostSaveMarker(DbContext context)
    {
        // Intentionally empty in this scaffolding milestone.
    }
}
