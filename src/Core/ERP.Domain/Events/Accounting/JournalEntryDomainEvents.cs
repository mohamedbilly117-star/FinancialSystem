using ERP.Domain.Common;

namespace ERP.Domain.Events.Accounting;

/// <summary>
/// Raised when a <c>JournalEntry</c> successfully transitions to
/// <see cref="Enums.JournalStatus.Posted"/>. Consumers dispatched after the
/// triggering SaveChanges commits (per
/// <c>AuditableEntitySaveChangesInterceptor</c>'s documented extension
/// point) may include: the Notification Engine (Prompt 10 - "Completion
/// Notifications"), the Audit Framework (Prompt 6 - "Posting" is an
/// explicitly listed auditable action), and, once built, any cached/
/// materialized General Ledger balance projection (Prompt 5 - "Running
/// Balance").
/// </summary>
public sealed class JournalEntryPostedDomainEvent : DomainEvent
{
    public Guid JournalEntryId { get; }

    public string JournalNumber { get; }

    public Guid FiscalYearId { get; }

    public Guid AccountingPeriodId { get; }

    public decimal TotalAmount { get; }

    public JournalEntryPostedDomainEvent(
        Guid journalEntryId,
        string journalNumber,
        Guid fiscalYearId,
        Guid accountingPeriodId,
        decimal totalAmount)
    {
        JournalEntryId = journalEntryId;
        JournalNumber = journalNumber;
        FiscalYearId = fiscalYearId;
        AccountingPeriodId = accountingPeriodId;
        TotalAmount = totalAmount;
    }
}

/// <summary>
/// Raised when a posted <c>JournalEntry</c> is reversed (Prompt 5 -
/// "Reversing entries"; Prompt 10 - Reversal Workflow: "Reason Tracking,
/// Audit Preservation"). Carries both the original and the newly created
/// reversing entry's identifiers so audit/notification consumers can link
/// the two without a separate lookup.
/// </summary>
public sealed class JournalEntryReversedDomainEvent : DomainEvent
{
    public Guid OriginalJournalEntryId { get; }

    public Guid ReversingJournalEntryId { get; }

    public string Reason { get; }

    public JournalEntryReversedDomainEvent(Guid originalJournalEntryId, Guid reversingJournalEntryId, string reason)
    {
        OriginalJournalEntryId = originalJournalEntryId;
        ReversingJournalEntryId = reversingJournalEntryId;
        Reason = reason;
    }
}
