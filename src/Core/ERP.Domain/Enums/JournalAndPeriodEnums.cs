namespace ERP.Domain.Enums;

/// <summary>
/// Journal entry lifecycle (Prompt 5 - Automatic Journal Engine: "Draft
/// journals. Posted journals. Rejected journals." + Prompt 10's Business
/// Process Lifecycle applied to the accounting domain). This is the
/// engine's own internal state machine - a system-controlled concept, not
/// an administrator-configurable business rule - so it is a fixed enum.
/// Valid transitions are enforced in <c>JournalEntry</c>'s domain methods,
/// never left to calling code to assemble.
/// </summary>
public enum JournalStatus
{
    Draft = 1,
    PendingApproval = 2,
    Approved = 3,
    Rejected = 4,
    Posted = 5,
    Reversed = 6,
    Cancelled = 7,
}

/// <summary>
/// Prompt 5 / Prompt 10 - Accounting Period Management: "Opening Period,
/// Closing Period, Locked Period, Reopened Period". A Fiscal Year has its
/// own top-level status independent of (but constraining) the status of
/// its individual Accounting Periods.
/// </summary>
public enum FiscalYearStatus
{
    Open = 1,
    Closed = 2,
    Locked = 3,
}

/// <summary>
/// Prompt 5 / Prompt 10 - Accounting Period Management: "Open, Closed,
/// Locked, Reopened ... Adjustment period". Posting is only ever allowed
/// into a period whose status is <see cref="Open"/> or
/// <see cref="Adjustment"/> - enforced by
/// <c>FiscalPeriodMustBeOpenRule</c>, never by scattered "if" checks.
/// </summary>
public enum AccountingPeriodStatus
{
    Open = 1,
    Closed = 2,
    Locked = 3,
    Reopened = 4,
    Adjustment = 5,
}
