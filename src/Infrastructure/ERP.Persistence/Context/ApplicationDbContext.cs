using ERP.Application.Common.Interfaces;
using ERP.Domain.Common;
using ERP.Domain.Entities.Accounting;
using ERP.Domain.Entities.Accounting.Distribution;
using ERP.Domain.Entities.Accounting.RuleEngine;
using ERP.Domain.Entities.Configuration;
using ERP.Domain.Entities.Security;
using ERP.Domain.Entities.Workflow;
using ERP.Security.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using System.Threading;

namespace ERP.Persistence.Context;

/// <summary>
/// The single EF Core DbContext for the entire ERP. Inherits
/// <see cref="IdentityDbContext{TUser,TRole,TKey}"/> to host ASP.NET Core
/// Identity's tables (Users, Roles, Claims, ...) in the SAME database as
/// every business table - this is a deliberate, simple choice appropriate
/// for a single-database, offline/LAN-deployed system (Prompt 0: "Offline,
/// No Cloud Dependency"); there is no need for a separate identity store.
///
/// Implements <see cref="IApplicationDbContext"/> so the Application layer
/// depends only on the abstraction (Dependency Inversion, Prompt 3).
///
/// DbSet&lt;T&gt; properties for approved business entities (Prompt 4's
/// Entity Catalogue) are added module-by-module starting with the
/// Accounting Foundation (FiscalYear/AccountingPeriod/Account/JournalEntry/
/// JournalEntryLine) below - the schema grows in lockstep with reviewed,
/// approved modules rather than being speculatively generated all at once.
/// </summary>
public class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // ===== Accounting Foundation (Prompt 4 / Prompt 5) =====
    public DbSet<FiscalYear> FiscalYears => Set<FiscalYear>();

    public DbSet<AccountingPeriod> AccountingPeriods => Set<AccountingPeriod>();

    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();

    public DbSet<JournalEntryLine> JournalEntryLines => Set<JournalEntryLine>();

    // ===== Distribution Engine (Prompt 5 / Prompt 11 addendum) =====
    public DbSet<DistributionTemplate> DistributionTemplates => Set<DistributionTemplate>();

    public DbSet<DistributionTemplateLine> DistributionTemplateLines => Set<DistributionTemplateLine>();

    // ===== Accounting Rule Engine (Prompt 5) =====
    public DbSet<AccountingRule> AccountingRules => Set<AccountingRule>();

    public DbSet<AccountingRuleCondition> AccountingRuleConditions => Set<AccountingRuleCondition>();

    // ===== Security (Prompt 6) =====
    public DbSet<Permission> Permissions => Set<Permission>();

    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();

    // ===== Workflow Engine (Prompt 10) =====
    public DbSet<WorkflowTemplate> WorkflowTemplates => Set<WorkflowTemplate>();

    public DbSet<ApprovalLevelDefinition> ApprovalLevelDefinitions => Set<ApprovalLevelDefinition>();

    public DbSet<WorkflowInstance> WorkflowInstances => Set<WorkflowInstance>();

    public DbSet<ApprovalAction> ApprovalActions => Set<ApprovalAction>();

    // ===== Configuration (Prompt 4 / Prompt 11) =====
    public DbSet<NumberingSequence> NumberingSequences => Set<NumberingSequence>();

    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

    public Task ReloadAsync(object entity, CancellationToken cancellationToken = default)
        => Entry(entity).ReloadAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Identity's own table mappings (AspNetUsers, AspNetRoles, ...).
        base.OnModelCreating(builder);

        // Every IEntityTypeConfiguration<T> in this assembly is picked up
        // automatically - each future module simply drops a configuration
        // class into Configurations/{ModuleName}/ and it is wired in
        // without touching this method again.
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // Rename Identity's default "AspNetXxx" tables to a consistent,
        // Arabic-first-friendly "Security" schema, keeping the business
        // schema (dbo, or future per-module schemas) visually and
        // administratively separate from authentication plumbing.
        builder.Entity<ApplicationUser>().ToTable("Users", schema: "security");
        builder.Entity<ApplicationRole>().ToTable("Roles", schema: "security");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("UserRoles", schema: "security");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaims", schema: "security");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogins", schema: "security");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("RoleClaims", schema: "security");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("UserTokens", schema: "security");

        ApplyGenericAuditableEntityConventions(builder);
        ApplySoftDeleteQueryFilters(builder);
    }

    /// <summary>
    /// Applies to EVERY entity type implementing <see cref="IAuditableEntity"/>,
    /// across every current and future module, without each module's own
    /// <c>IEntityTypeConfiguration&lt;T&gt;</c> needing to repeat it:
    /// maps <see cref="AuditableEntity.RowVersion"/> to SQL Server's native
    /// ROWVERSION column type and marks it as the optimistic concurrency
    /// token (Prompt 4 - Audit Data: "Version Number (for optimistic
    /// concurrency if appropriate)").
    /// </summary>
    private static void ApplyGenericAuditableEntityConventions(ModelBuilder builder)
    {
        foreach (var entityType in builder.Model.GetEntityTypes().ToList())
        {
            if (!typeof(IAuditableEntity).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            builder.Entity(entityType.ClrType)
                .Property(nameof(AuditableEntity.RowVersion))
                .IsRowVersion();
        }
    }

    /// <summary>
    /// Applies to EVERY entity type implementing <see cref="ISoftDelete"/>:
    /// a global query filter excluding logically-deleted rows from every
    /// normal LINQ query automatically (Prompt 4: "No financial transaction
    /// may be physically deleted. Soft Delete must be used where
    /// appropriate."). Queries that genuinely need deleted rows (e.g. an
    /// audit/history screen) use <c>IgnoreQueryFilters()</c> explicitly,
    /// making that an intentional, visible opt-in rather than the default.
    /// </summary>
    private static void ApplySoftDeleteQueryFilters(ModelBuilder builder)
    {
        foreach (var entityType in builder.Model.GetEntityTypes().ToList())
        {
            if (!typeof(ISoftDelete).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var property = Expression.Property(parameter, nameof(ISoftDelete.IsDeleted));
            var notDeleted = Expression.Not(property);
            var lambda = Expression.Lambda(notDeleted, parameter);

            builder.Entity(entityType.ClrType).HasQueryFilter(lambda);
        }
    }
}
