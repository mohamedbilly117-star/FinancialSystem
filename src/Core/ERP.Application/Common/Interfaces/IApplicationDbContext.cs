using ERP.Domain.Entities.Accounting;
using ERP.Domain.Entities.Accounting.Distribution;
using ERP.Domain.Entities.Accounting.RuleEngine;
using ERP.Domain.Entities.Configuration;
using ERP.Domain.Entities.Security;
using ERP.Domain.Entities.Workflow;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Common.Interfaces;

/// <summary>
/// The Application layer's view of the database - an abstraction over
/// <c>ERP.Persistence.Context.ApplicationDbContext</c> rather than a direct
/// reference to it, so Application depends on a contract instead of a
/// concrete Infrastructure type (Prompt 3's layering rule: Application may
/// depend on Domain and Shared, never on Persistence/Infrastructure/
/// Security/Workflow/Notifications/Reporting/Web).
///
/// Exposes <see cref="DbSet{TEntity}"/> directly (rather than a further,
/// hand-rolled repository/IQueryable wrapper) per Implementation
/// Clarification #1: "If EF Core already provides equivalent
/// functionality, avoid redundant wrapper implementations." This does pull
/// the core Microsoft.EntityFrameworkCore package into ERP.Application
/// (see ERP.Application.csproj for the precise scope of that reference) -
/// but never EF Core's SQL Server provider, migrations tooling, or the
/// concrete ApplicationDbContext class itself, which remain exclusive to
/// ERP.Persistence. This is the same pragmatic compromise used by the
/// reference Clean Architecture templates this solution's project
/// structure follows.
///
/// <c>ERP.Persistence.Context.ApplicationDbContext</c> implements this
/// interface. As each module is implemented (Database Layer / Domain Layer
/// milestones per Prompt 13), its DbSet&lt;T&gt; properties are added here
/// first (as the contract), then implemented on the concrete DbContext -
/// keeping the two in lockstep is enforced by the interface itself failing
/// to compile if they drift.
///
/// SaveChangesAsync is exposed here (rather than relying on a separate Unit
/// of Work abstraction wrapping it) per Implementation Clarification #1:
/// EF Core's DbContext already *is* the Unit of Work for a single logical
/// database, so no additional IUnitOfWork interface is introduced.
/// </summary>
public interface IApplicationDbContext
{
    /// <summary>
    /// Persists all pending changes tracked by the context in a single
    /// transaction. The Persistence-layer SaveChanges interceptor
    /// (AuditableEntitySaveChangesInterceptor) runs as part of this call to
    /// populate CreatedBy/CreatedAtUtc/ModifiedBy/ModifiedAtUtc automatically
    /// (Prompt 4 / Prompt 6 audit requirements) and to dispatch any pending
    /// Domain Events collected on saved entities.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Refreshes a tracked entity's property values (including any
    /// optimistic-concurrency token) from the database, discarding any
    /// unsaved in-memory changes on it. The one consumer today is
    /// <c>NumberingSequenceService</c>'s conflict-retry loop: after a
    /// <c>DbUpdateConcurrencyException</c>, the entity must be reloaded to
    /// the database's current state before re-attempting the operation
    /// against it - otherwise a retry would blindly repeat the exact same
    /// already-rejected write. Kept as a narrow, single-purpose addition
    /// rather than exposing EF Core's full ChangeTracker/EntityEntry API,
    /// which would pull far more of EF Core's concrete surface into
    /// ERP.Application than this one well-understood need justifies.
    /// </summary>
    Task ReloadAsync(object entity, CancellationToken cancellationToken = default);

    // ===== Accounting Foundation (Prompt 4 / Prompt 5) =====
    // First module implemented per Prompt 13's roadmap ("Database Layer /
    // Domain Layer" immediately before "Accounting Engine") - everything
    // else in the system ultimately posts through these.
    DbSet<FiscalYear> FiscalYears { get; }

    DbSet<AccountingPeriod> AccountingPeriods { get; }

    DbSet<Account> Accounts { get; }

    DbSet<JournalEntry> JournalEntries { get; }

    DbSet<JournalEntryLine> JournalEntryLines { get; }

    // ===== Distribution Engine (Prompt 5 / Prompt 11 addendum) =====
    DbSet<DistributionTemplate> DistributionTemplates { get; }

    DbSet<DistributionTemplateLine> DistributionTemplateLines { get; }

    // ===== Accounting Rule Engine (Prompt 5) =====
    DbSet<AccountingRule> AccountingRules { get; }

    DbSet<AccountingRuleCondition> AccountingRuleConditions { get; }

    // ===== Security (Prompt 6) =====
    DbSet<Permission> Permissions { get; }

    DbSet<RolePermission> RolePermissions { get; }

    DbSet<AuditLogEntry> AuditLogEntries { get; }

    // ===== Workflow Engine (Prompt 10) =====
    DbSet<WorkflowTemplate> WorkflowTemplates { get; }

    DbSet<ApprovalLevelDefinition> ApprovalLevelDefinitions { get; }

    DbSet<WorkflowInstance> WorkflowInstances { get; }

    DbSet<ApprovalAction> ApprovalActions { get; }

    // ===== Configuration (Prompt 4 / Prompt 11) =====
    DbSet<NumberingSequence> NumberingSequences { get; }

    DbSet<SystemSetting> SystemSettings { get; }

    // NOTE: DbSet<TEntity> properties for every other approved entity
    // (Prompt 4's Entity Catalogue - Banks, Contracts, Advances, Aid,
    // Cards, TreasuryBills, ...) are intentionally NOT declared yet. They
    // are added incrementally, one module at a time, in subsequent
    // milestones, so that this interface and the concrete
    // ApplicationDbContext are built up together with each module rather
    // than speculatively pre-declared.
}
