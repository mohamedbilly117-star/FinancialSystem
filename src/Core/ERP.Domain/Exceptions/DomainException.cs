using ERP.Domain.Interfaces;

namespace ERP.Domain.Exceptions;

/// <summary>
/// Thrown when a Domain invariant is violated inside an entity or value
/// object itself (e.g. attempting to construct a negative Money value, or
/// transitioning a transaction to an invalid lifecycle state per Prompt 10's
/// Business Process Lifecycle). This is distinct from
/// ERP.Application's ValidationException, which represents input validation
/// (FluentValidation) at the use-case boundary, before domain objects are
/// even constructed.
/// </summary>
public class DomainException : Exception
{
    public DomainException()
    {
    }

    public DomainException(string message)
        : base(message)
    {
    }

    public DomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Thrown when an explicit, named business rule (see <see cref="IBusinessRule"/>)
/// is not satisfied. Carries the failing rule so calling code / audit
/// logging can record precisely which configured rule blocked the
/// operation, per Prompt 6's requirement that every blocked action be
/// auditable and explainable.
/// </summary>
public sealed class BusinessRuleValidationException : DomainException
{
    public IBusinessRule BrokenRule { get; }

    public string Details { get; }

    public BusinessRuleValidationException(IBusinessRule brokenRule)
        : base(brokenRule.Message)
    {
        BrokenRule = brokenRule;
        Details = brokenRule.Message;
    }

    public override string ToString() => $"{BrokenRule.GetType().Name}: {Details}";
}
