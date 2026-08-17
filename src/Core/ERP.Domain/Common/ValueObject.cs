namespace ERP.Domain.Common;

/// <summary>
/// Base class for Value Objects (e.g. Money, Percentage, AccountCode,
/// DistributionShare) - immutable, compared by value rather than identity.
/// Future modules (Chart of Accounts, Distribution Engine, etc. - Prompt 5)
/// should model concepts like "a balanced monetary amount with currency"
/// as value objects rather than primitive decimals, so validation rules
/// (e.g. "amount must be non-negative", "percentage must be between 0 and
/// 100") live in one place and cannot be bypassed.
/// </summary>
public abstract class ValueObject : IEquatable<ValueObject>
{
    protected abstract IEnumerable<object?> GetEqualityComponents();

    public bool Equals(ValueObject? other) => Equals((object?)other);

    public override bool Equals(object? obj)
    {
        if (obj is null || obj.GetType() != GetType())
        {
            return false;
        }

        var other = (ValueObject)obj;
        return GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
    }

    public override int GetHashCode()
        => GetEqualityComponents()
            .Select(x => x?.GetHashCode() ?? 0)
            .Aggregate((x, y) => x ^ y);

    public static bool operator ==(ValueObject? left, ValueObject? right)
        => left is null ? right is null : left.Equals(right);

    public static bool operator !=(ValueObject? left, ValueObject? right) => !(left == right);
}
