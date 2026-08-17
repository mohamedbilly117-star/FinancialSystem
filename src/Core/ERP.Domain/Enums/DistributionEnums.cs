namespace ERP.Domain.Enums;

/// <summary>
/// Prompt 11 addendum: "Every revenue category, expense category,
/// activity, contract type, bank interest type, or financial source may
/// have its own independent distribution template." These six values are
/// the complete, exhaustive list from that addendum - deliberately not
/// open-ended, since which *kinds* of things can own a distribution
/// template is itself a fixed structural decision, not an administrator-
/// configurable business rule. Which specific instance within a category
/// (e.g. which Revenue Category) is referenced via
/// <see cref="Entities.Accounting.Distribution.DistributionTemplate.SourceReferenceId"/>
/// rather than by this enum.
/// </summary>
public enum DistributionSourceType
{
    RevenueCategory = 1,
    ExpenseCategory = 2,
    Activity = 3,
    ContractType = 4,
    BankInterestType = 5,
    FinancialSource = 6,
}

/// <summary>
/// Prompt 5 - Distribution Engine: "Percentage-based distribution, Fixed
/// amount distribution, Mixed distribution." ("No distribution" from that
/// same list is represented by the absence of an active template for a
/// given source, not a method value here - see
/// <see cref="Entities.Accounting.Distribution.DistributionTemplate"/>'s
/// class remarks.)
/// </summary>
public enum DistributionMethod
{
    Percentage = 1,
    FixedAmount = 2,
    Mixed = 3,
}
