using ERP.Domain.Common;
using ERP.Domain.Entities.Workflow.Rules;
using ERP.Domain.Enums;
using ERP.Domain.Exceptions;
using ERP.Shared.Guards;

namespace ERP.Domain.Entities.Workflow;

/// <summary>
/// Prompt 10 - Workflow Engine: "Workflow Templates... Approval Levels."
/// A configurable, ordered chain of approval levels for one
/// <see cref="SourceModuleCode"/> (e.g. "JournalEntry") - "requires
/// Section Head approval, then Director approval" becomes two
/// <see cref="ApprovalLevelDefinition"/> rows here.
///
/// Deliberately does NOT replace
/// <see cref="Accounting.JournalEntry"/>'s own existing Submit/Approve/
/// Reject/Post lifecycle (Prompt 5's single-step "Manual Approval Before
/// Posting" path, already built and left untouched per this milestone's
/// explicit instruction to reuse rather than duplicate). Instead, this is
/// an ADDITIVE capability: when a transaction's <see cref="AccountingRule"/>
/// requires MULTIPLE approval levels (not just one), a
/// <see cref="WorkflowInstance"/> tracks progress through this template's
/// levels; only once every required level has signed off does the
/// Application layer (a later milestone) call the underlying entity's own
/// final <c>Approve()</c>. A single-level template with one
/// <see cref="ApprovalLevelDefinition"/> is functionally equivalent to
/// today's direct single-step approval and is the expected default.
///
/// Follows the exact same versioning/activation pattern already
/// established by <see cref="Distribution.DistributionTemplate"/> and
/// <see cref="Accounting.RuleEngine.AccountingRule"/> for consistency.
/// </summary>
public sealed class WorkflowTemplate : AuditableEntity, IAggregateRoot, ISoftDelete
{
    private readonly List<ApprovalLevelDefinition> _levels = new();

    public string SourceModuleCode { get; private set; } = string.Empty;

    public string Code { get; private set; } = string.Empty;

    public string NameAr { get; private set; } = string.Empty;

    public string NameEn { get; private set; } = string.Empty;

    public int Version { get; private set; }

    public DateOnly EffectiveFrom { get; private set; }

    public DateOnly? EffectiveTo { get; private set; }

    public bool IsActive { get; private set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAtUtc { get; set; }

    public Guid? DeletedBy { get; set; }

    public IReadOnlyCollection<ApprovalLevelDefinition> Levels => _levels.AsReadOnly();

    private WorkflowTemplate()
    {
        // Required by EF Core.
    }

    private WorkflowTemplate(Guid id, string sourceModuleCode, string code, string nameAr, string nameEn, int version, DateOnly effectiveFrom)
    {
        Id = id;
        SourceModuleCode = sourceModuleCode;
        Code = code;
        NameAr = nameAr;
        NameEn = nameEn;
        Version = version;
        EffectiveFrom = effectiveFrom;
        IsActive = false;
    }

    public static WorkflowTemplate CreateFirstVersion(string sourceModuleCode, string code, string nameAr, string nameEn, DateOnly effectiveFrom)
    {
        Guard.AgainstNullOrWhiteSpace(sourceModuleCode, nameof(sourceModuleCode));
        Guard.AgainstLengthGreaterThan(sourceModuleCode, 50, nameof(sourceModuleCode));
        Guard.AgainstNullOrWhiteSpace(code, nameof(code));
        Guard.AgainstLengthGreaterThan(code, 30, nameof(code));
        Guard.AgainstNullOrWhiteSpace(nameAr, nameof(nameAr));
        Guard.AgainstNullOrWhiteSpace(nameEn, nameof(nameEn));

        return new WorkflowTemplate(Guid.NewGuid(), sourceModuleCode, code, nameAr, nameEn, 1, effectiveFrom);
    }

    /// <summary>
    /// Appends the next level in sequence (levels are always numbered
    /// 1..N in the order added - there is no separate "reorder" operation;
    /// reordering means removing and re-adding). <paramref name="minimumAmount"/>/
    /// <paramref name="maximumAmount"/> are Prompt 10's "Amount-Based
    /// Approval" configured as metadata on the level (e.g. "Level 2 only
    /// applies above 50,000") - stored and validated for internal
    /// consistency here, but the actual routing decision of which levels
    /// apply to a given transaction amount is deliberately left to the
    /// Application layer that starts a <see cref="WorkflowInstance"/>,
    /// not evaluated inside this Domain layer, to avoid conflating two
    /// different concerns (level ORDER vs. level APPLICABILITY) inside
    /// one piece of logic.
    /// </summary>
    public ApprovalLevelDefinition AddLevel(string nameAr, string nameEn, string requiredPermissionCode, decimal? minimumAmount = null, decimal? maximumAmount = null)
    {
        EnsureEditable();
        Guard.AgainstNullOrWhiteSpace(nameAr, nameof(nameAr));
        Guard.AgainstNullOrWhiteSpace(nameEn, nameof(nameEn));
        Guard.AgainstNullOrWhiteSpace(requiredPermissionCode, nameof(requiredPermissionCode));

        if (minimumAmount is not null && maximumAmount is not null && minimumAmount > maximumAmount)
        {
            throw new DomainException("An approval level's minimum amount cannot exceed its maximum amount.");
        }

        var levelNumber = _levels.Count + 1;
        var level = new ApprovalLevelDefinition(Id, levelNumber, nameAr, nameEn, requiredPermissionCode, minimumAmount, maximumAmount);
        _levels.Add(level);
        return level;
    }

    public void RemoveLastLevel()
    {
        EnsureEditable();

        if (_levels.Count == 0)
        {
            throw new DomainException($"Workflow template '{Code}' has no levels to remove.");
        }

        _levels.RemoveAt(_levels.Count - 1);
    }

    public void Activate()
    {
        var rule = new WorkflowTemplateMustHaveAtLeastOneLevelRule(_levels.Count);
        if (!rule.IsSatisfied())
        {
            throw new BusinessRuleValidationException(rule);
        }

        IsActive = true;
    }

    public void Deactivate() => IsActive = false;

    /// <summary>Mirrors <see cref="Distribution.DistributionTemplate.CreateNewVersion"/> and <see cref="Accounting.RuleEngine.AccountingRule.CreateNewVersion"/> exactly, for consistency across every versioned configuration entity in the solution.</summary>
    public WorkflowTemplate CreateNewVersion(string code, string nameAr, string nameEn, DateOnly newEffectiveFrom)
    {
        if (!IsActive)
        {
            throw new DomainException($"Only an Active workflow template can be superseded by a new version (template '{Code}' v{Version} is not Active).");
        }

        if (newEffectiveFrom <= EffectiveFrom)
        {
            throw new DomainException($"A new version's effective date ({newEffectiveFrom}) must be after the current version's effective date ({EffectiveFrom}).");
        }

        var newVersion = CreateFirstVersion(SourceModuleCode, code, nameAr, nameEn, newEffectiveFrom);
        newVersion.Version = Version + 1;

        EffectiveTo = newEffectiveFrom.AddDays(-1);

        return newVersion;
    }

    private void EnsureEditable()
    {
        if (IsActive)
        {
            throw new DomainException($"Workflow template '{Code}' v{Version} must be Deactivated before its levels can be modified.");
        }
    }
}

/// <summary>
/// One required approval step within a <see cref="WorkflowTemplate"/>.
/// <see cref="RequiredPermissionCode"/> is a bare string matching
/// <c>ERP.Domain.Entities.Security.Permission.Code</c>'s format (e.g.
/// "JournalEntry.Approve") - a forward reference, same reasoning as every
/// other cross-module string code in this solution: ERP.Domain must never
/// take a compile-time dependency on ERP.Security. The Application layer
/// checks "does this user hold this permission?" via
/// <c>IPermissionService.HasPermissionAsync</c> before allowing them to
/// act at this level.
/// </summary>
public sealed class ApprovalLevelDefinition : BaseEntity
{
    public Guid WorkflowTemplateId { get; private set; }

    public int LevelNumber { get; private set; }

    public string NameAr { get; private set; } = string.Empty;

    public string NameEn { get; private set; } = string.Empty;

    public string RequiredPermissionCode { get; private set; } = string.Empty;

    /// <summary>Prompt 10 - "Amount-Based Approval." Null means this level is not amount-gated.</summary>
    public decimal? MinimumAmount { get; private set; }

    public decimal? MaximumAmount { get; private set; }

    private ApprovalLevelDefinition()
    {
        // Required by EF Core.
    }

    internal ApprovalLevelDefinition(Guid workflowTemplateId, int levelNumber, string nameAr, string nameEn, string requiredPermissionCode, decimal? minimumAmount, decimal? maximumAmount)
    {
        Id = Guid.NewGuid();
        WorkflowTemplateId = workflowTemplateId;
        LevelNumber = levelNumber;
        NameAr = nameAr;
        NameEn = nameEn;
        RequiredPermissionCode = requiredPermissionCode;
        MinimumAmount = minimumAmount;
        MaximumAmount = maximumAmount;
    }
}
