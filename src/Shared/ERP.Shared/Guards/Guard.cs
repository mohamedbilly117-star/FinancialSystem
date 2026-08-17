namespace ERP.Shared.Guards;

/// <summary>
/// Small set of precondition helpers used throughout Domain entities,
/// Value Objects and Application use cases, so "required field" / "must be
/// positive" / "must not exceed length" checks (Prompt 2's Validation
/// Rules, Prompt 4's Required Fields) are expressed identically everywhere
/// instead of each module inventing its own ad-hoc null/range checks.
/// Throws <see cref="ArgumentException"/>/<see cref="ArgumentNullException"/>
/// - these represent programming errors (a caller passing bad data into a
/// constructor), which is different from a *business* rule violation
/// (see ERP.Domain.Exceptions.BusinessRuleValidationException) or a
/// *user input* validation failure (see ERP.Application's ValidationException).
/// </summary>
public static class Guard
{
    public static string AgainstNullOrWhiteSpace(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"'{parameterName}' must not be null, empty, or whitespace.", parameterName);
        }

        return value;
    }

    public static T AgainstNull<T>(T? value, string parameterName)
        where T : class
    {
        if (value is null)
        {
            throw new ArgumentNullException(parameterName);
        }

        return value;
    }

    public static Guid AgainstEmpty(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException($"'{parameterName}' must not be an empty Guid.", parameterName);
        }

        return value;
    }

    public static decimal AgainstNegative(decimal value, string parameterName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, $"'{parameterName}' must not be negative.");
        }

        return value;
    }

    public static decimal AgainstNegativeOrZero(decimal value, string parameterName)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, $"'{parameterName}' must be greater than zero.");
        }

        return value;
    }

    public static string AgainstLengthGreaterThan(string value, int maxLength, string parameterName)
    {
        if (value.Length > maxLength)
        {
            throw new ArgumentException($"'{parameterName}' must not exceed {maxLength} characters.", parameterName);
        }

        return value;
    }

    /// <summary>
    /// Enforces the Distribution Engine rule (Prompt 11 addendum): when a
    /// distribution template is percentage-based, its lines must sum to
    /// exactly 100%. Centralized here so every module building a
    /// distribution template (Revenue categories, Expense categories,
    /// Activities, Contract types, Bank interest types, ...) validates it
    /// identically rather than re-implementing the 100% check per module.
    /// </summary>
    public static void AgainstDistributionNotTotaling100Percent(decimal totalPercentage, string parameterName)
    {
        if (totalPercentage != 100m)
        {
            throw new ArgumentException(
                $"'{parameterName}' distribution percentages must total exactly 100%. Current total: {totalPercentage}%.",
                parameterName);
        }
    }
}
