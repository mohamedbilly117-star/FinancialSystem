namespace ERP.Domain.Common;

/// <summary>
/// Base class for every entity in the Domain model (Prompt 4 - Entity
/// Specification: every entity has a Primary Key). Uses a GUID surrogate
/// key rather than an identity/auto-increment integer so that:
///   - keys can be generated client-side/offline before a record is saved,
///     which matters for an offline-first, LAN-deployed system that may
///     someday need to merge data from multiple sites (Prompt 3 -
///     "Cloud Ready in the Future" / multi-organization support),
///   - primary keys never leak sequential business volume information
///     (a government financial system should not reveal "this is
///     transaction #4813 this year" through a URL or export file).
/// Also raises/collects Domain Events (Prompt 4/5: business operations can
/// have side effects - e.g. "RevenueRecorded" triggers the Accounting
/// Engine - without the entity needing to know about the Accounting Engine
/// directly).
/// </summary>
public abstract class BaseEntity : BaseEntity<Guid>
{
}

/// <summary>
/// Generic base entity for the rare cases where a non-Guid key is required
/// (e.g. a lookup/reference table keyed by a short stable code).
/// </summary>
public abstract class BaseEntity<TKey> : IHasDomainEvents
    where TKey : notnull
{
    public TKey Id { get; protected set; } = default!;

    private readonly List<DomainEvent> _domainEvents = new();

    /// <summary>
    /// Domain events raised by this entity but not yet dispatched.
    /// Dispatched by the Persistence layer's SaveChanges interceptor
    /// immediately after a successful commit, per Prompt 5's principle
    /// that "the ERP automatically generates accounting effects" as a
    /// consequence of business operations rather than direct user action.
    /// </summary>
    public IReadOnlyCollection<DomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void AddDomainEvent(DomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    protected void RemoveDomainEvent(DomainEvent domainEvent) => _domainEvents.Remove(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();

    public override bool Equals(object? obj)
    {
        if (obj is not BaseEntity<TKey> other)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (GetType() != other.GetType())
        {
            return false;
        }

        if (IsTransient(this) || IsTransient(other))
        {
            return false;
        }

        return Id.Equals(other.Id);
    }

    public static bool operator ==(BaseEntity<TKey>? left, BaseEntity<TKey>? right)
        => left is null ? right is null : left.Equals(right);

    public static bool operator !=(BaseEntity<TKey>? left, BaseEntity<TKey>? right) => !(left == right);

    public override int GetHashCode() => (GetType().ToString() + Id).GetHashCode();

    private static bool IsTransient(BaseEntity<TKey> entity)
        => entity.Id.Equals(default(TKey));
}
