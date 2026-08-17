namespace ERP.Domain.Interfaces;

/// <summary>
/// Contract for an explicit, self-describing business rule object.
/// The Business Rules Catalogue approved in Prompt 1 / Prompt 2 classifies
/// every rule as Mandatory / Configurable / Future Configurable / System
/// Controlled / User Controlled / Government Policy / Internal Policy.
/// Modeling rules as discrete IBusinessRule implementations (one class per
/// rule, e.g. "JournalMustBalanceRule", "FiscalPeriodMustBeOpenRule",
/// "DistributionTemplateMustTotal100PercentRule") - rather than scattering
/// "if" statements through service methods - keeps each rule:
///   - independently unit-testable,
///   - independently traceable back to the approved Business Rules
///     Catalogue (Requirements Traceability Matrix, Prompt 12),
///   - and safely reusable across every module that needs it, satisfying
///     Prompt 7's "never duplicate business logic" module principle.
/// </summary>
public interface IBusinessRule
{
    /// <summary>True when the rule is currently satisfied (i.e. no violation).</summary>
    bool IsSatisfied();

    /// <summary>Human-readable (Arabic/English localizable key) explanation shown to the user and written to the audit trail when the rule blocks an operation.</summary>
    string Message { get; }
}
