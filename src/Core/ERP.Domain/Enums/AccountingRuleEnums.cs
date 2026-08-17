namespace ERP.Domain.Enums;

/// <summary>
/// Prompt 5 - Accounting Rule Engine: "Conditions." How one
/// <c>AccountingRuleCondition</c> compares a named transaction field
/// against a configured value. Kept as a small, fixed comparison
/// vocabulary (rather than a general expression language) - enough to
/// express realistic government-ERP conditions ("Amount &gt;= 10000",
/// "OfficeCode = 'BANK-01'") without the scope and risk of building a full
/// expression parser/evaluator.
/// </summary>
public enum AccountingConditionOperator
{
    Equals = 1,
    NotEquals = 2,
    GreaterThan = 3,
    GreaterThanOrEqual = 4,
    LessThan = 5,
    LessThanOrEqual = 6,

    /// <summary>Inclusive on both ends: satisfied when Value &lt;= field &lt;= ValueTo.</summary>
    Between = 7,
}

/// <summary>
/// Prompt 5 - Accounting Rule Engine lists "Conditions." and "Exceptions."
/// as two separate configurable concerns. Modeled here as a discriminator
/// on the same <c>AccountingRuleCondition</c> shape (rather than two
/// duplicated entity types) so a rule can express both: it applies when
/// every <see cref="Match"/> condition is satisfied AND no
/// <see cref="Exception"/> condition is satisfied - e.g. "applies to every
/// Bank Office transaction (Match), EXCEPT when the amount exceeds 50,000
/// (Exception)."
/// </summary>
public enum AccountingConditionKind
{
    Match = 1,
    Exception = 2,
}
