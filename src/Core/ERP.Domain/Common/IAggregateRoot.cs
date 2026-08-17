namespace ERP.Domain.Common;

/// <summary>
/// Marks an entity as the root of an Aggregate (DDD) - the only entity
/// within a consistency boundary that repositories/the Application layer
/// are allowed to load and save directly. Child entities within the same
/// aggregate (e.g. JournalEntryLine under JournalEntry, ContractInstallment
/// under Contract, AdvanceInstallment under Advance - all present in the
/// approved Entity Catalogue, Prompt 4) are only ever reached through their
/// aggregate root, which is what lets the root enforce invariants such as
/// "a JournalEntry's lines must always balance" (Prompt 5) at the single
/// point where the aggregate is saved.
/// This is a marker interface only - it carries no members - future
/// aggregate roots simply add ": IAggregateRoot" to their class declaration.
/// </summary>
public interface IAggregateRoot
{
}
