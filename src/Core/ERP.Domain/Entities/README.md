# ERP.Domain / Entities

This folder intentionally contains no entities yet.

Per the approved **Implementation Master Plan (Prompt 13)**, entities are
introduced module-by-module, in the order defined by the *Module
Implementation Sequence* deliverable, starting with the Database Layer /
Domain Layer phase immediately after this scaffolding milestone.

## Progress

- [x] **Accounting** - `FiscalYear`, `AccountingPeriod`, `Account` (Chart of
      Accounts), `JournalEntry`, `JournalEntryLine`, plus the
      `Entities/Accounting/Rules/` business rules
      (`JournalMustBalanceRule`, `JournalMustHaveAtLeastTwoLinesRule`,
      `FiscalPeriodMustBeOpenForPostingRule`, `AccountMustAllowPostingRule`,
      `JournalLineMustHaveExactlyOneSideRule`) and
      `Events/Accounting/` domain events
      (`JournalEntryPostedDomainEvent`, `JournalEntryReversedDomainEvent`).
      EF Core configurations live in
      `ERP.Persistence/Configurations/Accounting/`; DbSets are exposed on
      `IApplicationDbContext` / `ApplicationDbContext`. Covered by
      `ERP.UnitTests/Domain/Accounting/JournalEntryTests.cs`.
- [x] **Distribution Engine** (Prompt 5 / Prompt 11 addendum) -
      `Entities/Accounting/Distribution/DistributionTemplate` +
      `DistributionTemplateLine`: independent per-(SourceType,
      SourceReferenceId) templates, unlimited destination lines,
      Percentage/FixedAmount/Mixed methods, effective-dated version
      history (`CreateNewVersion`), explicit Activate/Deactivate
      lifecycle, and the 100%-total check (scoped to Percentage-method
      templates only, per the addendum's own wording) via
      `Guard.AgainstDistributionNotTotaling100Percent`. "Automatic
      selection of the correct template" is enforced as unambiguous at
      the database level by a unique filtered index
      (`IX_DistributionTemplates_ActiveSource` in
      `ERP.Persistence/Configurations/Distribution/`) rather than in
      the Domain layer, since it requires querying sibling rows; the
      actual lookup query is Application-layer work for a later
      milestone. Covered by
      `ERP.UnitTests/Domain/Distribution/DistributionTemplateTests.cs`.
- [x] **Accounting Rule Engine** (Prompt 5) -
      `Entities/Accounting/RuleEngine/AccountingRule` +
      `AccountingRuleCondition`: Debit/Credit mapping to either a fixed
      `Account` or delegation to the Distribution Engine
      (`DistributionSourceType`), `Priority`-based resolution among
      multiple simultaneously-Active rules per `SourceModuleCode`,
      `Conditions`/`Exceptions` (Match vs Exception condition kind) with
      Equals/NotEquals/comparison/Between operators, approval-requirement
      flag, and the same effective-dated version-history pattern as
      `DistributionTemplate`. The pure, stateless
      `AccountingRuleResolver` domain service performs actual rule
      selection (priority + condition evaluation) without any database
      dependency. EF configurations in
      `ERP.Persistence/Configurations/RuleEngine/`, including a unique
      filtered index preventing priority ties among Active rules per
      module. Covered by
      `ERP.UnitTests/Domain/RuleEngine/AccountingRuleTests.cs` and
      `AccountingRuleResolverTests.cs`.
- [x] **Account Balance Rules** (Prompt 5) -
      `Entities/Accounting/Balances/AccountBalanceSnapshot` (a
      `ValueObject` - the first real use of that base class) +
      `AccountBalanceCalculator`: pure, stateless computation of Opening
      Balance, Debit/Credit Movement, Closing Balance, and a
      `RunningBalancePoint` sequence (Running Balance), correctly
      interpreting movement direction from `Account.NormalBalance`
      (Debit-normal vs Credit-normal accounts). Deliberately NOT an
      entity and NEVER persisted as its own table - always freshly
      derived from `JournalEntryLine` data (no `AccountBalances` DbSet
      anywhere), so "Monthly/Yearly/Historical Balance" are the same
      calculation over a caller-date-filtered line set, not separate
      mechanisms. No EF Core changes needed for this milestone (nothing
      to persist). Covered by
      `ERP.UnitTests/Domain/Balances/AccountBalanceCalculatorTests.cs`.
- [x] **Security - Permission Matrix & Audit Log** (Prompt 6) -
      `Entities/Security/Permission` (system-defined catalog) +
      `RolePermission` (the actual Role↔Permission matrix; `RoleId` is a
      bare `Guid` forward-reference to `ApplicationRole` in ERP.Security,
      with the real FK added in
      `ERP.Persistence/Configurations/Security/RolePermissionConfiguration`
      - the one place in the solution that can see both sides) +
      `AuditLogEntry` (general-purpose event log - Login/Logout/Failed
      Login/entity changes/permission & role changes - distinct from
      `AuditableEntity`'s per-entity CreatedBy/ModifiedBy fields).
      `IPermissionService`/`PermissionService` (resolves User → Roles →
      granted Permission codes via `UserManager`/`RoleManager` +
      `IApplicationDbContext`) and `IAuditService`/`AuditService`
      (authentication events self-persist; entity/permission-change
      events are staged only, so they commit atomically with the
      business change they accompany) complete the previously-scaffolded
      `PermissionAuthorizationHandler`/`PermissionPolicyProvider`
      infrastructure. `ERP.Security/Permissions/AccountingPermissions.cs`
      is the concrete permission catalog protecting every sensitive
      Accounting Engine operation (approve/post/reverse/close/reopen/
      configure). Covered by
      `ERP.UnitTests/Domain/Security/PermissionTests.cs`,
      `AuditLogEntryTests.cs`, and
      `ERP.IntegrationTests/Security/AuditServiceTests.cs`.
- [x] **Workflow Engine - Multi-Level Approval Chains** (Prompt 10) -
      `Entities/Workflow/WorkflowTemplate` (+ `ApprovalLevelDefinition`)
      and `WorkflowInstance` (+ `ApprovalAction`). Deliberately ADDITIVE:
      `Accounting.JournalEntry`'s own existing single-step
      Submit/Approve/Reject/Post lifecycle is completely untouched (still
      has all 5 of its original public methods, verified this session) -
      a `WorkflowInstance` tracks progress through a template's ordered
      approval levels for transactions that require MORE than one
      approval; a single-level template is functionally equivalent to
      today's direct single-step approval. Each
      `ApprovalLevelDefinition.RequiredPermissionCode` is a forward-
      referencing string matching `Security.Permission.Code`'s exact
      format (e.g. `"JournalEntry.Approve"`) - the Application layer (a
      later milestone) checks it via `IPermissionService`. Rejection at
      any level is terminal (matches `JournalEntry.Reject`'s own
      terminal-state semantics) and always requires a reason. Follows the
      identical versioning/activation pattern as `DistributionTemplate`
      and `AccountingRule` for consistency. Deliberately does NOT
      auto-write `AuditLogEntry` rows from within the Domain entities
      themselves (same reasoning as every other module: Domain layer
      never calls Infrastructure/Security services directly - that
      wiring is `IAuditService`'s explicit job from the Application layer,
      not yet built). EF configurations in
      `ERP.Persistence/Configurations/Workflow/`. Covered by
      `ERP.UnitTests/Domain/Workflow/WorkflowTemplateTests.cs` (11 tests)
      and `WorkflowInstanceTests.cs` (12 tests).
- [x] **Configuration - Numbering & System Settings** (Prompt 4 / Prompt 11) -
      `Entities/Configuration/NumberingSequence`: the configuration data
      `ERP.Shared.Constants.NumberingSequenceKeys`'s own doc comment
      already anticipated, closing the real gap that nothing previously
      generated the journal number `JournalEntry.Post` accepts. Exposes
      `MaxPrefixLength` as a single shared constant referenced by both
      the Domain guard and the EF Core column mapping (and by the tests'
      own boundary cases) so the two constraints - and the tests - cannot
      silently drift apart. `Entities/Configuration/SystemSetting`:
      generic runtime-editable key-value configuration (Prompt 11 System
      Configuration/System Parameters) rather than dozens of speculative
      strongly-typed settings entities; duplicate-key prevention is
      correctly a persistence-level concern (EF unique index), not a
      Domain one, and is tested as such.
      `INumberingSequenceService`/`NumberingSequenceService`
      (ERP.Infrastructure) is genuinely concurrency-safe, not just
      documented as such: on `DbUpdateConcurrencyException` it reloads
      the entity via the new minimal `IApplicationDbContext.ReloadAsync`
      member and retries (max 5 attempts) before converting to a clear
      `InvalidOperationException` - verified with a deterministic fault-
      injecting `ApplicationDbContext` subclass rather than relying on
      EF Core InMemory's uncertain RowVersion auto-generation semantics.
      Deferred, deliberately: Report Configuration and Notification
      Templates (Prompt 11) - both depend on modules (Reporting,
      Notifications) that don't exist yet. Covered by
      `ERP.UnitTests/Domain/Configuration/NumberingSequenceTests.cs`,
      `SystemSettingTests.cs`, and
      `ERP.IntegrationTests/Configuration/NumberingSequenceServiceTests.cs`,
      `SystemSettingPersistenceTests.cs` (32 tests total this phase).
- [ ] Identity (organizational), Banking, Treasury, HigherAuthorityFunds,
      Revenue, Expenses, Contracts, Activities, Advances, Aid, Cards,
      Checks, Transfers, Workflow, Notifications, Audit, Configuration -
      not yet started.

Every entity created here must trace back to the approved **Entity
Catalogue (Prompt 4 - Enterprise Database Design & Data Architecture)** and
follow the sub-folder-per-module convention, e.g.:

```
Entities/
  Identity/          (organizational, not authentication - Departments, Offices, Entities, Companies, Persons)
  Banking/            Banks, BankAccounts
  Treasury/           Treasury, TreasuryDeposits, TreasuryBills, InterestRecords
  HigherAuthorityFunds/
  Accounting/         ChartOfAccounts, JournalEntry, JournalEntryLine, FiscalYear, AccountingPeriod
  Revenue/            RevenueSources, RevenueTransactions
  Expenses/           ExpenseTypes, ExpenseTransactions
  DistributionEngine/ DistributionTemplate, DistributionTemplateLine
  Contracts/          Contracts, ContractInstallments
  Activities/
  Advances/           Advances, AdvanceInstallments
  Aid/                AidTypes, AidPayments
  Cards/              BankCards, CardHolders
  Checks/
  Transfers/
  Workflow/           WorkflowInstance, ApprovalLevel
  Notifications/
  Audit/              AuditLog
  Configuration/      NumberingRules, ConfigurationTables
```

Every entity added here must:
1. Inherit `AuditableEntity` (or `BaseEntity` directly only if it is a pure,
   immutable reference/lookup row that is never user-modified after seed).
2. Implement `ISoftDelete` if the approved Database Design marks it as a
   record that must never be physically deleted (this applies to virtually
   all financial transaction tables per Prompt 4's "no financial
   transaction may be physically deleted" rule).
3. Be configured via a dedicated EF Core `IEntityTypeConfiguration<T>` class
   in `ERP.Persistence/Configurations/` - **never** via data annotations on
   the entity itself, to keep persistence concerns out of the Domain layer.
