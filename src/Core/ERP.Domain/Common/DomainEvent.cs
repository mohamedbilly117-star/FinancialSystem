namespace ERP.Domain.Common;

/// <summary>
/// Marker/base for anything that raises domain events (currently only
/// <see cref="BaseEntity{TKey}"/>, kept as a separate interface so the
/// Application layer can depend on this abstraction without depending on
/// the concrete BaseEntity type).
/// </summary>
public interface IHasDomainEvents
{
    IReadOnlyCollection<DomainEvent> DomainEvents { get; }

    void ClearDomainEvents();
}

/// <summary>
/// Base class for all domain events (e.g. RevenueRecordedEvent,
/// AdvanceSettledEvent, JournalPostedEvent). Concrete events live inside
/// each business module (built in later milestones per Prompt 13's phase
/// plan) and are dispatched, after a successful SaveChanges, to handlers
/// that may - among other things - instruct the Accounting Engine
/// (Prompt 5) to generate the corresponding balanced journal entry.
/// Domain events are the mechanism that keeps "users perform business
/// operations; the ERP automatically generates accounting journals"
/// (Prompt 0 / Prompt 5) decoupled from the entities themselves.
/// </summary>
public abstract class DomainEvent
{
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
