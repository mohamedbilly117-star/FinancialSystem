using ERP.Domain.Enums;
using ERP.Domain.Interfaces;

namespace ERP.Domain.Entities.Accounting.Rules;

/// <summary>
/// Prompt 5 - Accounting Rule Engine: "Debit account. Credit account.
/// Distribution logic." Each side of an <c>AccountingRule</c> (Debit or
/// Credit) must be resolved exactly one way: either a single fixed
/// <c>Account</c>, or delegated to the Distribution Engine for a given
/// <see cref="DistributionSourceType"/> - never both (ambiguous), never
/// neither (unresolved, the journal could never be built).
/// </summary>
public sealed class AccountingRuleSideMustBeResolvedExactlyOneWayRule : IBusinessRule
{
    private readonly string _sideName;
    private readonly Guid? _fixedAccountId;
    private readonly DistributionSourceType? _distributionSourceType;

    public AccountingRuleSideMustBeResolvedExactlyOneWayRule(string sideName, Guid? fixedAccountId, DistributionSourceType? distributionSourceType)
    {
        _sideName = sideName;
        _fixedAccountId = fixedAccountId;
        _distributionSourceType = distributionSourceType;
    }

    public bool IsSatisfied() => (_fixedAccountId is not null) ^ (_distributionSourceType is not null);

    public string Message => (_fixedAccountId, _distributionSourceType) switch
    {
        (not null, not null) => $"The {_sideName} side cannot be both a fixed account and Distribution-resolved at the same time - choose one.",
        (null, null) => $"The {_sideName} side must be resolved either by a fixed account or by the Distribution Engine - neither was specified.",
        _ => $"The {_sideName} side is resolved correctly.", // unreachable when IsSatisfied() is false, kept for exhaustiveness.
    };
}

/// <summary>
/// A rule that debits and credits the exact same fixed account is either a
/// configuration mistake or a no-op journal - never a valid Accounting
/// Rule. Only meaningful (and only checked) when BOTH sides resolve to a
/// fixed account; when either side is Distribution-resolved this rule does
/// not apply (the two sides cannot be trivially compared).
/// </summary>
public sealed class AccountingRuleDebitAndCreditAccountsMustDifferRule : IBusinessRule
{
    private readonly Guid? _debitAccountId;
    private readonly Guid? _creditAccountId;

    public AccountingRuleDebitAndCreditAccountsMustDifferRule(Guid? debitAccountId, Guid? creditAccountId)
    {
        _debitAccountId = debitAccountId;
        _creditAccountId = creditAccountId;
    }

    public bool IsSatisfied()
        => _debitAccountId is null || _creditAccountId is null || _debitAccountId != _creditAccountId;

    public string Message => "An accounting rule's Debit and Credit fixed accounts must not be the same account.";
}

/// <summary>
/// Prompt 5 - Accounting Rule Engine: "Conditions. Exceptions." A
/// condition's stored Value(s) must actually be usable by its declared
/// <see cref="AccountingConditionOperator"/>: <see cref="AccountingConditionOperator.Between"/>
/// requires both a lower and upper bound; every operator other than
/// Equals/NotEquals is a numeric comparison and therefore requires its
/// Value (and ValueTo, for Between) to parse as a decimal - a condition
/// that could never evaluate correctly against any real transaction is
/// rejected at configuration time rather than silently failing later.
/// </summary>
public sealed class AccountingRuleConditionMustBeValidRule : IBusinessRule
{
    private readonly AccountingConditionOperator _operator;
    private readonly string _value;
    private readonly string? _valueTo;

    public AccountingRuleConditionMustBeValidRule(AccountingConditionOperator @operator, string value, string? valueTo)
    {
        _operator = @operator;
        _value = value;
        _valueTo = valueTo;
    }

    public bool IsSatisfied()
    {
        if (_operator == AccountingConditionOperator.Between)
        {
            return decimal.TryParse(_value, out _) && !string.IsNullOrWhiteSpace(_valueTo) && decimal.TryParse(_valueTo, out _);
        }

        var isNumericOperator = _operator is AccountingConditionOperator.GreaterThan
            or AccountingConditionOperator.GreaterThanOrEqual
            or AccountingConditionOperator.LessThan
            or AccountingConditionOperator.LessThanOrEqual;

        return !isNumericOperator || decimal.TryParse(_value, out _);
    }

    public string Message => _operator == AccountingConditionOperator.Between
        ? "A 'Between' condition requires both Value and ValueTo to be present and parseable as numbers."
        : $"A '{_operator}' condition requires Value to be parseable as a number.";
}
