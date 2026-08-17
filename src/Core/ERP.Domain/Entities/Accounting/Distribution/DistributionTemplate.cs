using ERP.Domain.Common;
using ERP.Domain.Entities.Accounting.Rules;
using ERP.Domain.Enums;
using ERP.Domain.Exceptions;
using ERP.Shared.Guards;

namespace ERP.Domain.Entities.Accounting.Distribution;

// This file's namespace (ERP.Domain.Entities.Accounting.Distribution) is a
// child of ERP.Domain.Entities.Accounting, but C# namespaces are not
// hierarchically visible to each other automatically - Account
// (referenced directly below, e.g. as the AddLine parameter type) needs
// its own explicit "using" back up to that namespace even though this
// file's own namespace is nested underneath it.
using ERP.Domain.Entities.Accounting;

/// <summary>
/// Prompt 5 (Distribution Engine) + Prompt 11 addendum (the authoritative,
/// most specific source for this entity). Represents ONE version of the
/// distribution configuration for exactly one specific instance of exactly
/// one <see cref="DistributionSourceType"/> - e.g. "how Revenue Category
/// 'Parking Fees' is split across GL accounts, effective 2026-01-01."
///
/// Per the addendum, explicitly enforced here:
///   - "Every revenue category, expense category, activity, contract type,
///     bank interest type, or financial source may have its own
///     independent distribution template" -&gt; <see cref="SourceType"/> +
///     <see cref="SourceReferenceId"/> together identify exactly which
///     instance this template belongs to; nothing here is shared/global.
///   - "unlimited destination accounts" -&gt; <see cref="Lines"/> has no
///     upper bound.
///   - "configurable percentages... effective dates... version history...
///     activation/deactivation" -&gt; <see cref="Version"/>,
///     <see cref="EffectiveFrom"/>/<see cref="EffectiveTo"/>,
///     <see cref="IsActive"/>, and <see cref="CreateNewVersion"/>.
///   - "validation that the total allocation equals 100% when
///     percentage-based" -&gt; enforced in <see cref="Activate"/> via
///     <see cref="Guard.AgainstDistributionNotTotaling100Percent"/>, and
///     ONLY when <see cref="Method"/> is <see cref="DistributionMethod.Percentage"/>
///     (the addendum's own wording scopes the 100% rule to that case; a
///     Mixed or FixedAmount template is not required to total 100%, since
///     fixed-amount lines are not percentages of anything).
///   - "automatic selection of the correct template during transaction
///     processing" -&gt; this is a query/lookup concern (find the Active
///     template for a given SourceType+SourceReferenceId+date), which
///     requires database access and therefore belongs to the Application/
///     Infrastructure layer (a future <c>IDistributionTemplateResolver</c>),
///     not to this Domain entity - see the Persistence configuration's
///     unique filtered index, which is what makes that lookup unambiguous.
///   - "No global distribution percentages may be assumed" -&gt; there is no
///     default/fallback template anywhere in this entity or its
///     configuration; a source with no Active template simply has no
///     distribution (Prompt 5's explicit "No distribution" case).
/// </summary>
public sealed class DistributionTemplate : AuditableEntity, IAggregateRoot, ISoftDelete
{
    private readonly List<DistributionTemplateLine> _lines = new();

    public DistributionSourceType SourceType { get; private set; }

    /// <summary>
    /// Identifies exactly which instance of <see cref="SourceType"/> this
    /// template belongs to (e.g. a specific Revenue Category's Id).
    /// Forward-referenced (not yet a real foreign key) - the concrete
    /// Revenue/Expense/Activity/Contract/BankInterestType/FinancialSource
    /// master-data tables are built in a later Master Data milestone; this
    /// field is what those tables' Ids will populate once they exist,
    /// exactly mirroring how <c>JournalEntry.SourceReferenceId</c> already
    /// forward-references not-yet-built transactional tables.
    /// </summary>
    public Guid SourceReferenceId { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string NameAr { get; private set; } = string.Empty;

    public string NameEn { get; private set; } = string.Empty;

    public DistributionMethod Method { get; private set; }

    public int Version { get; private set; }

    public DateOnly EffectiveFrom { get; private set; }

    /// <summary>Null = open-ended (still the current version as far as this row alone is concerned; superseding is expressed by a later version's own EffectiveFrom, not by mutating this to non-null automatically - see <see cref="CreateNewVersion"/>).</summary>
    public DateOnly? EffectiveTo { get; private set; }

    /// <summary>Administrative on/off switch (Prompt 11 addendum: "activation/deactivation"), independent of the effective-date window - a template can be date-current but explicitly deactivated, or date-expired but still flagged active for historical reference.</summary>
    public bool IsActive { get; private set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAtUtc { get; set; }

    public Guid? DeletedBy { get; set; }

    public IReadOnlyCollection<DistributionTemplateLine> Lines => _lines.AsReadOnly();

    private DistributionTemplate()
    {
        // Required by EF Core.
    }

    private DistributionTemplate(Guid id, DistributionSourceType sourceType, Guid sourceReferenceId, string code, string nameAr, string nameEn, DistributionMethod method, int version, DateOnly effectiveFrom)
    {
        Id = id;
        SourceType = sourceType;
        SourceReferenceId = sourceReferenceId;
        Code = code;
        NameAr = nameAr;
        NameEn = nameEn;
        Method = method;
        Version = version;
        EffectiveFrom = effectiveFrom;
        IsActive = false; // Must pass Activate() (which validates lines/total) before it can be selected by any transaction.
    }

    /// <summary>Starts a brand-new template lineage (Version 1) for a given source instance. Lines are added afterward via <see cref="AddLine"/>, then <see cref="Activate"/> makes it usable.</summary>
    public static DistributionTemplate CreateFirstVersion(DistributionSourceType sourceType, Guid sourceReferenceId, string code, string nameAr, string nameEn, DistributionMethod method, DateOnly effectiveFrom)
    {
        Guard.AgainstEmpty(sourceReferenceId, nameof(sourceReferenceId));
        Guard.AgainstNullOrWhiteSpace(code, nameof(code));
        Guard.AgainstLengthGreaterThan(code, 30, nameof(code));
        Guard.AgainstNullOrWhiteSpace(nameAr, nameof(nameAr));
        Guard.AgainstNullOrWhiteSpace(nameEn, nameof(nameEn));

        return new DistributionTemplate(Guid.NewGuid(), sourceType, sourceReferenceId, code, nameAr, nameEn, method, 1, effectiveFrom);
    }

    /// <summary>
    /// Supersedes this (currently Active) template with a new, independent
    /// Draft version sharing the same <see cref="SourceType"/> and
    /// <see cref="SourceReferenceId"/> (Prompt 11 addendum: "version
    /// history"). This version's own <see cref="EffectiveTo"/> is closed
    /// out to the day before the new version begins; this version's
    /// <see cref="IsActive"/> flag is deliberately left untouched (it
    /// remains administratively active - only its effective date window
    /// now excludes future dates) so historical lookups for dates within
    /// its own window keep working unchanged.
    /// </summary>
    public DistributionTemplate CreateNewVersion(string code, string nameAr, string nameEn, DistributionMethod method, DateOnly newEffectiveFrom)
    {
        if (!IsActive)
        {
            throw new DomainException($"Only an Active distribution template can be superseded by a new version (template '{Code}' v{Version} is not Active).");
        }

        if (newEffectiveFrom <= EffectiveFrom)
        {
            throw new DomainException($"A new version's effective date ({newEffectiveFrom}) must be after the current version's effective date ({EffectiveFrom}).");
        }

        var newVersion = CreateFirstVersion(SourceType, SourceReferenceId, code, nameAr, nameEn, method, newEffectiveFrom);
        newVersion.Version = Version + 1;

        EffectiveTo = newEffectiveFrom.AddDays(-1);

        return newVersion;
    }

    /// <summary>
    /// Adds one destination line. <paramref name="percentage"/> and
    /// <paramref name="fixedAmount"/> are validated against this
    /// template's <see cref="Method"/> via
    /// <see cref="DistributionLineMustMatchTemplateMethodRule"/> - e.g. a
    /// Percentage-method template rejects a line that only specifies a
    /// fixed amount.
    /// </summary>
    public DistributionTemplateLine AddLine(Account destinationAccount, decimal? percentage, decimal? fixedAmount, string? descriptionAr = null, string? descriptionEn = null)
    {
        EnsureEditable();
        Guard.AgainstNull(destinationAccount, nameof(destinationAccount));
        destinationAccount.EnsureCanReceivePosting();

        var methodRule = new DistributionLineMustMatchTemplateMethodRule(Method, percentage, fixedAmount);
        if (!methodRule.IsSatisfied())
        {
            throw new BusinessRuleValidationException(methodRule);
        }

        var line = new DistributionTemplateLine(Id, _lines.Count + 1, destinationAccount.Id, percentage, fixedAmount, descriptionAr, descriptionEn);
        _lines.Add(line);
        return line;
    }

    public void RemoveLine(Guid lineId)
    {
        EnsureEditable();

        var line = _lines.SingleOrDefault(l => l.Id == lineId);
        if (line is null)
        {
            throw new DomainException($"Distribution template '{Code}' v{Version} has no line with id '{lineId}'.");
        }

        _lines.Remove(line);
    }

    /// <summary>
    /// Makes the template usable by the (future) automatic selection
    /// lookup. Enforces the addendum's explicit invariants: at least one
    /// line, and - only for <see cref="DistributionMethod.Percentage"/> -
    /// the lines' percentages must total exactly 100%.
    /// </summary>
    public void Activate()
    {
        var lineCountRule = new DistributionTemplateMustHaveAtLeastOneLineRule(_lines.Count);
        if (!lineCountRule.IsSatisfied())
        {
            throw new BusinessRuleValidationException(lineCountRule);
        }

        if (Method == DistributionMethod.Percentage)
        {
            var totalPercentage = _lines.Sum(l => l.Percentage ?? 0m);
            Guard.AgainstDistributionNotTotaling100Percent(totalPercentage, nameof(totalPercentage));
        }

        IsActive = true;
    }

    /// <summary>Administratively withdraws this template from selection without deleting it (Prompt 11 addendum: "activation/deactivation"). Lines become editable again afterward.</summary>
    public void Deactivate() => IsActive = false;

    private void EnsureEditable()
    {
        if (IsActive)
        {
            throw new DomainException($"Distribution template '{Code}' v{Version} must be Deactivated before its lines can be modified.");
        }
    }
}

/// <summary>
/// One destination account within a <see cref="DistributionTemplate"/>.
/// Child entity - only ever created/removed through the owning template,
/// exactly like <see cref="JournalEntryLine"/> within
/// <see cref="JournalEntry"/>.
/// </summary>
public sealed class DistributionTemplateLine : BaseEntity
{
    public Guid DistributionTemplateId { get; private set; }

    public int LineNumber { get; private set; }

    public Guid DestinationAccountId { get; private set; }

    /// <summary>Populated when the owning template's Method is Percentage or Mixed. A value of 25.5 means 25.5%.</summary>
    public decimal? Percentage { get; private set; }

    /// <summary>Populated when the owning template's Method is FixedAmount or Mixed.</summary>
    public decimal? FixedAmount { get; private set; }

    public string? DescriptionAr { get; private set; }

    public string? DescriptionEn { get; private set; }

    private DistributionTemplateLine()
    {
        // Required by EF Core.
    }

    internal DistributionTemplateLine(Guid distributionTemplateId, int lineNumber, Guid destinationAccountId, decimal? percentage, decimal? fixedAmount, string? descriptionAr, string? descriptionEn)
    {
        if (percentage is < 0)
        {
            throw new DomainException("A distribution line's percentage cannot be negative.");
        }

        if (fixedAmount is < 0)
        {
            throw new DomainException("A distribution line's fixed amount cannot be negative.");
        }

        Id = Guid.NewGuid();
        DistributionTemplateId = distributionTemplateId;
        LineNumber = lineNumber;
        DestinationAccountId = Guard.AgainstEmpty(destinationAccountId, nameof(destinationAccountId));
        Percentage = percentage;
        FixedAmount = fixedAmount;
        DescriptionAr = descriptionAr;
        DescriptionEn = descriptionEn;
    }
}
