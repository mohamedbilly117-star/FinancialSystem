using ERP.Domain.Common;
using ERP.Domain.Entities.Accounting.Rules;
using ERP.Domain.Enums;
using ERP.Domain.Exceptions;
using ERP.Shared.Guards;

namespace ERP.Domain.Entities.Accounting.RuleEngine;

using ERP.Domain.Entities.Accounting; // Account: this file's namespace is nested under ERP.Domain.Entities.Accounting, which is not automatically visible without this explicit using (same pattern as Distribution/DistributionTemplate.cs).

/// <summary>
/// Prompt 5 - Accounting Rule Engine: "Design a configurable accounting
/// rule engine. Rules must never be hardcoded. Administrators must be able
/// to configure: Debit account. Credit account. Distribution logic.
/// Conditions. Exceptions. Approval requirements. Future accounting
/// changes."
///
/// One <see cref="AccountingRule"/> answers, for a given
/// <see cref="SourceModuleCode"/> (a business event/process, e.g.
/// "REVENUE_COLLECTION" - matching the same free-form code convention
/// already used by <see cref="JournalEntry.SourceModuleCode"/>): which
/// account(s) get debited and credited, and whether posting needs manual
/// approval first. Multiple rules MAY legitimately be Active for the same
/// <see cref="SourceModuleCode"/> at once (unlike
/// <see cref="Distribution.DistributionTemplate"/>, which allows only one
/// Active template per source) - that is exactly what
/// <see cref="Priority"/> and <see cref="Conditions"/>/
/// <see cref="Exceptions"/> are for: a general rule plus one or more
/// narrower override rules for special cases. Given a set of candidate
/// rules and a transaction's field values, <see cref="AccountingRuleResolver"/>
/// (a separate, stateless Domain Service) determines which single rule
/// actually applies.
///
/// "Distribution logic" is deliberately NOT re-implemented here - each
/// side (Debit/Credit) either names one fixed <see cref="Account"/>, or
/// is flagged as resolved by the already-built Distribution Engine for a
/// given <see cref="DistributionSourceType"/> (see
/// <see cref="DebitDistributionSourceType"/>/<see cref="CreditDistributionSourceType"/>) -
/// the actual per-transaction distribution split still goes through
/// <see cref="Distribution.DistributionTemplate"/>, keyed by the
/// transaction's own category reference at posting time (an Application-
/// layer lookup, exactly like <see cref="Distribution.DistributionTemplate"/>'s
/// own "automatic selection" note).
/// </summary>
public sealed class AccountingRule : AuditableEntity, IAggregateRoot, ISoftDelete
{
    private readonly List<AccountingRuleCondition> _conditions = new();

    public string SourceModuleCode { get; private set; } = string.Empty;

    public string Code { get; private set; } = string.Empty;

    public string NameAr { get; private set; } = string.Empty;

    public string NameEn { get; private set; } = string.Empty;

    /// <summary>Lower value = higher precedence. When more than one Active rule for the same <see cref="SourceModuleCode"/> matches a transaction, <see cref="AccountingRuleResolver"/> selects the one with the lowest Priority.</summary>
    public int Priority { get; private set; }

    /// <summary>Descriptive only (does not change resolution behavior beyond what Priority/Conditions already achieve) - marks this rule as an override of a more general rule, per Prompt 5's explicit "Exceptions" concept, for audit/UI clarity ("this posting used an exception rule, not the standard one").</summary>
    public bool IsException { get; private set; }

    public Guid? DebitAccountId { get; private set; }

    public DistributionSourceType? DebitDistributionSourceType { get; private set; }

    public Guid? CreditAccountId { get; private set; }

    public DistributionSourceType? CreditDistributionSourceType { get; private set; }

    public bool RequiresApprovalBeforePosting { get; private set; }

    public int Version { get; private set; }

    public DateOnly EffectiveFrom { get; private set; }

    public DateOnly? EffectiveTo { get; private set; }

    public bool IsActive { get; private set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAtUtc { get; set; }

    public Guid? DeletedBy { get; set; }

    public IReadOnlyCollection<AccountingRuleCondition> Conditions => _conditions.AsReadOnly();

    public IEnumerable<AccountingRuleCondition> MatchConditions => _conditions.Where(c => c.Kind == AccountingConditionKind.Match);

    public IEnumerable<AccountingRuleCondition> Exceptions => _conditions.Where(c => c.Kind == AccountingConditionKind.Exception);

    private AccountingRule()
    {
        // Required by EF Core.
    }

    private AccountingRule(
        Guid id,
        string sourceModuleCode,
        string code,
        string nameAr,
        string nameEn,
        int priority,
        bool isException,
        Guid? debitAccountId,
        DistributionSourceType? debitDistributionSourceType,
        Guid? creditAccountId,
        DistributionSourceType? creditDistributionSourceType,
        bool requiresApprovalBeforePosting,
        int version,
        DateOnly effectiveFrom)
    {
        Id = id;
        SourceModuleCode = sourceModuleCode;
        Code = code;
        NameAr = nameAr;
        NameEn = nameEn;
        Priority = priority;
        IsException = isException;
        DebitAccountId = debitAccountId;
        DebitDistributionSourceType = debitDistributionSourceType;
        CreditAccountId = creditAccountId;
        CreditDistributionSourceType = creditDistributionSourceType;
        RequiresApprovalBeforePosting = requiresApprovalBeforePosting;
        Version = version;
        EffectiveFrom = effectiveFrom;
        IsActive = false; // Must pass Activate() before it can be selected by AccountingRuleResolver.
    }

    /// <summary>
    /// Starts a brand-new rule lineage (Version 1). Exactly one of
    /// (<paramref name="debitAccount"/>, <paramref name="debitDistributionSourceType"/>)
    /// and exactly one of (<paramref name="creditAccount"/>,
    /// <paramref name="creditDistributionSourceType"/>) must be provided -
    /// validated immediately (not deferred to <see cref="Activate"/>) since
    /// an unresolved side is a construction-time configuration error, not
    /// something that becomes valid once conditions are added.
    /// </summary>
    public static AccountingRule CreateFirstVersion(
        string sourceModuleCode,
        string code,
        string nameAr,
        string nameEn,
        int priority,
        bool isException,
        Account? debitAccount,
        DistributionSourceType? debitDistributionSourceType,
        Account? creditAccount,
        DistributionSourceType? creditDistributionSourceType,
        bool requiresApprovalBeforePosting,
        DateOnly effectiveFrom)
    {
        Guard.AgainstNullOrWhiteSpace(sourceModuleCode, nameof(sourceModuleCode));
        Guard.AgainstLengthGreaterThan(sourceModuleCode, 50, nameof(sourceModuleCode));
        Guard.AgainstNullOrWhiteSpace(code, nameof(code));
        Guard.AgainstLengthGreaterThan(code, 30, nameof(code));
        Guard.AgainstNullOrWhiteSpace(nameAr, nameof(nameAr));
        Guard.AgainstNullOrWhiteSpace(nameEn, nameof(nameEn));

        var debitAccountId = ResolveFixedAccount(debitAccount);
        var creditAccountId = ResolveFixedAccount(creditAccount);

        var debitRule = new AccountingRuleSideMustBeResolvedExactlyOneWayRule("Debit", debitAccountId, debitDistributionSourceType);
        if (!debitRule.IsSatisfied())
        {
            throw new BusinessRuleValidationException(debitRule);
        }

        var creditRule = new AccountingRuleSideMustBeResolvedExactlyOneWayRule("Credit", creditAccountId, creditDistributionSourceType);
        if (!creditRule.IsSatisfied())
        {
            throw new BusinessRuleValidationException(creditRule);
        }

        var differRule = new AccountingRuleDebitAndCreditAccountsMustDifferRule(debitAccountId, creditAccountId);
        if (!differRule.IsSatisfied())
        {
            throw new BusinessRuleValidationException(differRule);
        }

        return new AccountingRule(
            Guid.NewGuid(),
            sourceModuleCode,
            code,
            nameAr,
            nameEn,
            priority,
            isException,
            debitAccountId,
            debitDistributionSourceType,
            creditAccountId,
            creditDistributionSourceType,
            requiresApprovalBeforePosting,
            1,
            effectiveFrom);
    }

    private static Guid? ResolveFixedAccount(Account? account)
    {
        if (account is null)
        {
            return null;
        }

        account.EnsureCanReceivePosting();
        return account.Id;
    }

    /// <summary>Prompt 5 - "Conditions." All Match conditions must be satisfied for this rule to apply to a transaction.</summary>
    public AccountingRuleCondition AddMatchCondition(string fieldName, AccountingConditionOperator @operator, string value, string? valueTo = null)
        => AddCondition(AccountingConditionKind.Match, fieldName, @operator, value, valueTo);

    /// <summary>Prompt 5 - "Exceptions." If ANY Exception condition is satisfied, this rule does NOT apply, even if every Match condition is satisfied.</summary>
    public AccountingRuleCondition AddExceptionCondition(string fieldName, AccountingConditionOperator @operator, string value, string? valueTo = null)
        => AddCondition(AccountingConditionKind.Exception, fieldName, @operator, value, valueTo);

    private AccountingRuleCondition AddCondition(AccountingConditionKind kind, string fieldName, AccountingConditionOperator @operator, string value, string? valueTo)
    {
        EnsureEditable();

        var validityRule = new AccountingRuleConditionMustBeValidRule(@operator, value, valueTo);
        if (!validityRule.IsSatisfied())
        {
            throw new BusinessRuleValidationException(validityRule);
        }

        var condition = new AccountingRuleCondition(Id, kind, fieldName, @operator, value, valueTo);
        _conditions.Add(condition);
        return condition;
    }

    public void RemoveCondition(Guid conditionId)
    {
        EnsureEditable();

        var condition = _conditions.SingleOrDefault(c => c.Id == conditionId);
        if (condition is null)
        {
            throw new DomainException($"Accounting rule '{Code}' v{Version} has no condition with id '{conditionId}'.");
        }

        _conditions.Remove(condition);
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    /// <summary>Mirrors <see cref="Distribution.DistributionTemplate.CreateNewVersion"/> exactly: supersedes this Active rule with a new Draft version sharing the same <see cref="SourceModuleCode"/>, closing this version's effective date window without touching its IsActive flag.</summary>
    public AccountingRule CreateNewVersion(
        string code,
        string nameAr,
        string nameEn,
        int priority,
        bool isException,
        Account? debitAccount,
        DistributionSourceType? debitDistributionSourceType,
        Account? creditAccount,
        DistributionSourceType? creditDistributionSourceType,
        bool requiresApprovalBeforePosting,
        DateOnly newEffectiveFrom)
    {
        if (!IsActive)
        {
            throw new DomainException($"Only an Active accounting rule can be superseded by a new version (rule '{Code}' v{Version} is not Active).");
        }

        if (newEffectiveFrom <= EffectiveFrom)
        {
            throw new DomainException($"A new version's effective date ({newEffectiveFrom}) must be after the current version's effective date ({EffectiveFrom}).");
        }

        var newVersion = CreateFirstVersion(
            SourceModuleCode,
            code,
            nameAr,
            nameEn,
            priority,
            isException,
            debitAccount,
            debitDistributionSourceType,
            creditAccount,
            creditDistributionSourceType,
            requiresApprovalBeforePosting,
            newEffectiveFrom);
        newVersion.Version = Version + 1;

        EffectiveTo = newEffectiveFrom.AddDays(-1);

        return newVersion;
    }

    private void EnsureEditable()
    {
        if (IsActive)
        {
            throw new DomainException($"Accounting rule '{Code}' v{Version} must be Deactivated before its conditions can be modified.");
        }
    }
}

/// <summary>
/// One condition (Match) or exception (Exception) attached to an
/// <see cref="AccountingRule"/>. <see cref="FieldName"/> names a business
/// fact from the transaction context the future
/// <c>IAccountingRuleContextProvider</c> (Application layer) supplies at
/// resolution time - e.g. "Amount", "OfficeCode" - the set of valid names
/// is defined by whichever module raises the accounting event, not fixed
/// here.
/// </summary>
public sealed class AccountingRuleCondition : BaseEntity
{
    public Guid AccountingRuleId { get; private set; }

    public AccountingConditionKind Kind { get; private set; }

    public string FieldName { get; private set; } = string.Empty;

    public AccountingConditionOperator Operator { get; private set; }

    public string Value { get; private set; } = string.Empty;

    /// <summary>Only populated (and only meaningful) when <see cref="Operator"/> is <see cref="AccountingConditionOperator.Between"/> - the inclusive upper bound.</summary>
    public string? ValueTo { get; private set; }

    private AccountingRuleCondition()
    {
        // Required by EF Core.
    }

    internal AccountingRuleCondition(Guid accountingRuleId, AccountingConditionKind kind, string fieldName, AccountingConditionOperator @operator, string value, string? valueTo)
    {
        Id = Guid.NewGuid();
        AccountingRuleId = accountingRuleId;
        Kind = kind;
        FieldName = Guard.AgainstNullOrWhiteSpace(fieldName, nameof(fieldName));
        Operator = @operator;
        Value = Guard.AgainstNullOrWhiteSpace(value, nameof(value));
        ValueTo = valueTo;
    }

    /// <summary>
    /// Evaluates this single condition against a transaction's field
    /// values. Fail-safe, not fail-crash: a missing field, or a field
    /// value that cannot be parsed as required by <see cref="Operator"/>,
    /// is treated as "not satisfied" rather than throwing - a data-quality
    /// gap elsewhere in the system must not crash the posting pipeline;
    /// it should simply fail to match this (or any) rule, which the
    /// resolver's caller is responsible for handling (e.g. falling back to
    /// manual posting).
    /// </summary>
    public bool IsSatisfiedBy(IReadOnlyDictionary<string, string> context)
    {
        if (!context.TryGetValue(FieldName, out var rawFieldValue))
        {
            return false;
        }

        if (Operator is AccountingConditionOperator.Equals or AccountingConditionOperator.NotEquals)
        {
            var isEqual = string.Equals(rawFieldValue, Value, StringComparison.Ordinal);
            return Operator == AccountingConditionOperator.Equals ? isEqual : !isEqual;
        }

        if (!decimal.TryParse(rawFieldValue, out var fieldNumericValue) || !decimal.TryParse(Value, out var conditionNumericValue))
        {
            return false;
        }

        return Operator switch
        {
            AccountingConditionOperator.GreaterThan => fieldNumericValue > conditionNumericValue,
            AccountingConditionOperator.GreaterThanOrEqual => fieldNumericValue >= conditionNumericValue,
            AccountingConditionOperator.LessThan => fieldNumericValue < conditionNumericValue,
            AccountingConditionOperator.LessThanOrEqual => fieldNumericValue <= conditionNumericValue,
            AccountingConditionOperator.Between when ValueTo is not null && decimal.TryParse(ValueTo, out var upperBound)
                => fieldNumericValue >= conditionNumericValue && fieldNumericValue <= upperBound,
            _ => false,
        };
    }
}
