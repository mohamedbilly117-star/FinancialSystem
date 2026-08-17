using ERP.Domain.Enums;
using ERP.Domain.Interfaces;

namespace ERP.Domain.Entities.Accounting.Rules;

/// <summary>
/// Prompt 11 addendum: "Distribution templates must support unlimited
/// destination accounts" - implying at least one is required for a
/// template to be usable. Mirrors
/// <see cref="JournalMustHaveAtLeastTwoLinesRule"/>'s pattern but with a
/// minimum of one, since a distribution template (unlike a journal entry)
/// is not itself a double-entry construct.
/// </summary>
public sealed class DistributionTemplateMustHaveAtLeastOneLineRule : IBusinessRule
{
    private readonly int _lineCount;

    public DistributionTemplateMustHaveAtLeastOneLineRule(int lineCount) => _lineCount = lineCount;

    public bool IsSatisfied() => _lineCount >= 1;

    public string Message => $"A distribution template must have at least one destination line; this template has {_lineCount}.";
}

/// <summary>
/// A distribution line's populated amount(s) must match its parent
/// template's <see cref="DistributionMethod"/>: a Percentage template's
/// lines must specify a percentage (and never a fixed amount); a
/// FixedAmount template's lines must specify a fixed amount (and never a
/// percentage); a Mixed template's lines must specify at least one of the
/// two. This prevents a line from silently carrying a value that the
/// template's method will never actually apply.
/// </summary>
public sealed class DistributionLineMustMatchTemplateMethodRule : IBusinessRule
{
    private readonly DistributionMethod _method;
    private readonly decimal? _percentage;
    private readonly decimal? _fixedAmount;

    public DistributionLineMustMatchTemplateMethodRule(DistributionMethod method, decimal? percentage, decimal? fixedAmount)
    {
        _method = method;
        _percentage = percentage;
        _fixedAmount = fixedAmount;
    }

    public bool IsSatisfied() => _method switch
    {
        DistributionMethod.Percentage => _percentage is > 0 && _fixedAmount is null,
        DistributionMethod.FixedAmount => _fixedAmount is > 0 && _percentage is null,
        DistributionMethod.Mixed => (_percentage is > 0) || (_fixedAmount is > 0),
        _ => false,
    };

    public string Message => _method switch
    {
        DistributionMethod.Percentage => "A Percentage-method distribution line must specify a percentage greater than zero and must not specify a fixed amount.",
        DistributionMethod.FixedAmount => "A FixedAmount-method distribution line must specify a fixed amount greater than zero and must not specify a percentage.",
        DistributionMethod.Mixed => "A Mixed-method distribution line must specify a percentage and/or a fixed amount greater than zero.",
        _ => $"Unrecognized distribution method '{_method}'.",
    };
}
