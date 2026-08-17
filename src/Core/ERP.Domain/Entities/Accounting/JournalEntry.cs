using ERP.Domain.Common;
using ERP.Domain.Entities.Accounting.Rules;
using ERP.Domain.Enums;
using ERP.Domain.Events.Accounting;
using ERP.Domain.Exceptions;
using ERP.Shared.Guards;

namespace ERP.Domain.Entities.Accounting;

/// <summary>
/// The Automatic Journal Engine's core aggregate (Prompt 5). Every
/// financial operation performed anywhere in the ERP is expected to
/// eventually produce one of these - never a manually hand-built,
/// unvalidated set of debits and credits. Enforces, as invariants that
/// cannot be bypassed by any caller:
///   - lines may only be added/removed while still in Draft (a Submitted/
///     Posted entry is immutable, matching Prompt 4's "financial history
///     must never be lost"),
///   - a line may only target an <see cref="Account"/> that is active and
///     not a Parent (summary) account (<see cref="AccountMustAllowPostingRule"/>),
///   - the entry must have at least two lines and its debits must equal
///     its credits before it may leave Draft
///     (<see cref="JournalMustHaveAtLeastTwoLinesRule"/>,
///     <see cref="JournalMustBalanceRule"/>),
///   - posting requires the target accounting period to currently allow
///     posting (<see cref="FiscalPeriodMustBeOpenForPostingRule"/>),
///   - once <see cref="JournalStatus.Posted"/>, an entry can only be
///     corrected by creating a new reversing entry
///     (<see cref="CreateReversal"/>) - it is never edited or deleted in
///     place (Prompt 5: "Reversing entries" rather than mutation).
///
/// Supports both configured posting modes from Prompt 5 ("Automatic
/// Posting" and "Manual Approval Before Posting"): <see cref="Post"/> may
/// be called directly from <see cref="JournalStatus.Draft"/> (automatic
/// posting, no human approval step) or from
/// <see cref="JournalStatus.Approved"/> (after <see cref="Submit"/> +
/// <see cref="Approve"/>) - which path a given transaction takes is a
/// configured Accounting Rule Engine decision (Prompt 5/11), not something
/// this entity decides for itself.
/// </summary>
public sealed class JournalEntry : AuditableEntity, IAggregateRoot, ISoftDelete
{
    private readonly List<JournalEntryLine> _lines = new();

    /// <summary>
    /// Null until <see cref="Post"/> assigns the final, gap-free sequential
    /// number (Prompt 4 - Numbering Strategy, key
    /// <see cref="ERP.Shared.Constants.NumberingSequenceKeys.JournalNumber"/>).
    /// Deliberately not assigned at Draft creation, so that Draft/Rejected/
    /// Cancelled entries that never reach Posted never consume a permanent
    /// journal number - keeping the posted sequence itself gap-free for
    /// audit purposes.
    /// </summary>
    public string? JournalNumber { get; private set; }

    public Guid FiscalYearId { get; private set; }

    public Guid AccountingPeriodId { get; private set; }

    public DateOnly EntryDate { get; private set; }

    public JournalStatus Status { get; private set; }

    public string DescriptionAr { get; private set; } = string.Empty;

    public string DescriptionEn { get; private set; } = string.Empty;

    /// <summary>Which module/event produced this entry (e.g. "REVENUE", "EXPENSE", "MANUAL") - a simple free-form code for this milestone; formalized as a full reference/master-data table when the Accounting Event Catalogue (Prompt 5) modules are built.</summary>
    public string? SourceModuleCode { get; private set; }

    /// <summary>Id of the originating business transaction (e.g. a future RevenueTransaction.Id), if this entry was system-generated rather than manually created.</summary>
    public Guid? SourceReferenceId { get; private set; }

    public Guid? ApprovedBy { get; private set; }

    public DateTime? ApprovedAtUtc { get; private set; }

    public Guid? PostedBy { get; private set; }

    public DateTime? PostedAtUtc { get; private set; }

    /// <summary>Populated on Reject() and reused on Cancel() - the human-readable reason recorded for audit (Prompt 6: "Reason").</summary>
    public string? RejectionReason { get; private set; }

    /// <summary>Set only on a reversing entry, pointing back at the original Posted entry it reverses (Prompt 5: "Reversing entries").</summary>
    public Guid? ReversalOfJournalEntryId { get; private set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAtUtc { get; set; }

    public Guid? DeletedBy { get; set; }

    public IReadOnlyCollection<JournalEntryLine> Lines => _lines.AsReadOnly();

    public decimal TotalDebit => _lines.Sum(l => l.DebitAmount);

    public decimal TotalCredit => _lines.Sum(l => l.CreditAmount);

    private JournalEntry()
    {
        // Required by EF Core.
    }

    private JournalEntry(Guid id, Guid fiscalYearId, Guid accountingPeriodId, DateOnly entryDate, string descriptionAr, string descriptionEn, string? sourceModuleCode, Guid? sourceReferenceId)
    {
        Id = id;
        FiscalYearId = fiscalYearId;
        AccountingPeriodId = accountingPeriodId;
        EntryDate = entryDate;
        DescriptionAr = descriptionAr;
        DescriptionEn = descriptionEn;
        SourceModuleCode = sourceModuleCode;
        SourceReferenceId = sourceReferenceId;
        Status = JournalStatus.Draft;
    }

    public static JournalEntry CreateDraft(
        Guid fiscalYearId,
        Guid accountingPeriodId,
        DateOnly entryDate,
        string descriptionAr,
        string descriptionEn,
        string? sourceModuleCode = null,
        Guid? sourceReferenceId = null)
    {
        Guard.AgainstEmpty(fiscalYearId, nameof(fiscalYearId));
        Guard.AgainstEmpty(accountingPeriodId, nameof(accountingPeriodId));
        Guard.AgainstNullOrWhiteSpace(descriptionAr, nameof(descriptionAr));
        Guard.AgainstNullOrWhiteSpace(descriptionEn, nameof(descriptionEn));

        return new JournalEntry(Guid.NewGuid(), fiscalYearId, accountingPeriodId, entryDate, descriptionAr, descriptionEn, sourceModuleCode, sourceReferenceId);
    }

    /// <summary>
    /// Adds one debit-or-credit line against <paramref name="account"/>.
    /// Takes the full <see cref="Account"/> (not just its Id) so this
    /// aggregate can enforce <see cref="AccountMustAllowPostingRule"/>
    /// itself rather than trusting the caller to have checked it - only
    /// <c>account.Id</c> is actually persisted on the resulting line.
    /// </summary>
    public JournalEntryLine AddLine(Account account, decimal debitAmount, decimal creditAmount, string? descriptionAr = null, string? descriptionEn = null)
    {
        EnsureDraft();
        Guard.AgainstNull(account, nameof(account));
        account.EnsureCanReceivePosting();

        var line = new JournalEntryLine(Id, _lines.Count + 1, account.Id, debitAmount, creditAmount, descriptionAr, descriptionEn);
        _lines.Add(line);
        return line;
    }

    public void RemoveLine(Guid lineId)
    {
        EnsureDraft();

        var line = _lines.SingleOrDefault(l => l.Id == lineId);
        if (line is null)
        {
            throw new DomainException($"Journal entry '{JournalNumber ?? Id.ToString()}' has no line with id '{lineId}'.");
        }

        _lines.Remove(line);
        RenumberLines();
    }

    /// <summary>Moves the entry into the approval queue (Prompt 5/10 - "Manual Approval Before Posting" path). Not required for the "Automatic Posting" path - see <see cref="Post"/>.</summary>
    public void Submit()
    {
        EnsureDraft();
        EnsureBalanced();
        Status = JournalStatus.PendingApproval;
    }

    public void Approve(Guid approvedByUserId, DateTime approvedAtUtc)
    {
        if (Status != JournalStatus.PendingApproval)
        {
            throw new DomainException($"Journal entry must be Pending Approval to be approved (current status: {Status}).");
        }

        EnsureBalanced();

        ApprovedBy = Guard.AgainstEmpty(approvedByUserId, nameof(approvedByUserId));
        ApprovedAtUtc = approvedAtUtc;
        Status = JournalStatus.Approved;
    }

    public void Reject(string reason)
    {
        if (Status != JournalStatus.PendingApproval)
        {
            throw new DomainException($"Journal entry must be Pending Approval to be rejected (current status: {Status}).");
        }

        RejectionReason = Guard.AgainstNullOrWhiteSpace(reason, nameof(reason));
        Status = JournalStatus.Rejected;
    }

    /// <summary>A Draft, Pending Approval or Rejected entry may be withdrawn entirely (Prompt 10 - Business Process Lifecycle: "Cancelled"). A Posted entry cannot be cancelled - see <see cref="CreateReversal"/> instead.</summary>
    public void Cancel(string reason)
    {
        if (Status is not (JournalStatus.Draft or JournalStatus.PendingApproval or JournalStatus.Rejected))
        {
            throw new DomainException(
                $"Only a Draft, Pending Approval or Rejected journal entry can be cancelled (current status: {Status}). " +
                "A Posted entry must be reversed instead.");
        }

        RejectionReason = Guard.AgainstNullOrWhiteSpace(reason, nameof(reason));
        Status = JournalStatus.Cancelled;
    }

    /// <summary>
    /// Posts the entry, permanently affecting account balances. Callable
    /// from <see cref="JournalStatus.Draft"/> (Automatic Posting) or
    /// <see cref="JournalStatus.Approved"/> (after the manual approval
    /// path) - see the class-level remarks. <paramref name="journalNumber"/>
    /// is supplied by the caller (an <c>INumberingSequenceService</c>
    /// implementation, built in the Configuration/Numbering milestone) -
    /// this entity only validates and stores it, never generates it.
    /// </summary>
    public void Post(Guid postedByUserId, DateTime postedAtUtc, string journalNumber, AccountingPeriodStatus currentPeriodStatus)
    {
        if (Status is not (JournalStatus.Draft or JournalStatus.Approved))
        {
            throw new DomainException(
                $"Only a Draft (automatic posting) or Approved (manual approval) journal entry can be posted (current status: {Status}).");
        }

        EnsureBalanced();

        var periodRule = new FiscalPeriodMustBeOpenForPostingRule(currentPeriodStatus);
        if (!periodRule.IsSatisfied())
        {
            throw new BusinessRuleValidationException(periodRule);
        }

        var assignedJournalNumber = Guard.AgainstNullOrWhiteSpace(journalNumber, nameof(journalNumber));

        JournalNumber = assignedJournalNumber;
        PostedBy = Guard.AgainstEmpty(postedByUserId, nameof(postedByUserId));
        PostedAtUtc = postedAtUtc;
        Status = JournalStatus.Posted;

        AddDomainEvent(new JournalEntryPostedDomainEvent(Id, assignedJournalNumber, FiscalYearId, AccountingPeriodId, TotalDebit));
    }

    /// <summary>
    /// Creates a new Draft journal entry that exactly mirrors this one with
    /// every line's debit/credit swapped (Prompt 5 - "Reversing entries").
    /// The reversal is returned as a Draft rather than force-posted
    /// immediately, so it still passes through the normal Submit/Approve/
    /// Post lifecycle - satisfying Prompt 10's explicit "Reversal Approval"
    /// requirement rather than silently bypassing it.
    /// </summary>
    public JournalEntry CreateReversal(string reason, Guid targetAccountingPeriodId, DateOnly reversalEntryDate)
    {
        if (Status != JournalStatus.Posted)
        {
            throw new DomainException($"Only a Posted journal entry can be reversed (current status: {Status}).");
        }

        Guard.AgainstNullOrWhiteSpace(reason, nameof(reason));
        Guard.AgainstEmpty(targetAccountingPeriodId, nameof(targetAccountingPeriodId));

        var reversalDescriptionAr = $"عكس قيد رقم {JournalNumber}: {reason}";
        var reversalDescriptionEn = $"Reversal of journal {JournalNumber}: {reason}";

        var reversal = new JournalEntry(
            Guid.NewGuid(),
            FiscalYearId,
            targetAccountingPeriodId,
            reversalEntryDate,
            reversalDescriptionAr,
            reversalDescriptionEn,
            SourceModuleCode,
            SourceReferenceId)
        {
            ReversalOfJournalEntryId = Id,
        };

        var lineNumber = 1;
        foreach (var originalLine in _lines)
        {
            // Debit/credit swapped: this is what makes the reversal cancel out the original's effect on account balances.
            reversal._lines.Add(new JournalEntryLine(reversal.Id, lineNumber, originalLine.AccountId, originalLine.CreditAmount, originalLine.DebitAmount, originalLine.DescriptionAr, originalLine.DescriptionEn));
            lineNumber++;
        }

        Status = JournalStatus.Reversed;

        AddDomainEvent(new JournalEntryReversedDomainEvent(Id, reversal.Id, reason));

        return reversal;
    }

    private void EnsureDraft()
    {
        if (Status != JournalStatus.Draft)
        {
            throw new DomainException($"Journal entry lines can only be added or removed while the entry is in Draft status (current status: {Status}).");
        }
    }

    private void EnsureBalanced()
    {
        var lineCountRule = new JournalMustHaveAtLeastTwoLinesRule(_lines.Count);
        if (!lineCountRule.IsSatisfied())
        {
            throw new BusinessRuleValidationException(lineCountRule);
        }

        var balanceRule = new JournalMustBalanceRule(TotalDebit, TotalCredit);
        if (!balanceRule.IsSatisfied())
        {
            throw new BusinessRuleValidationException(balanceRule);
        }
    }

    private void RenumberLines()
    {
        var number = 1;
        foreach (var line in _lines)
        {
            line.Renumber(number);
            number++;
        }
    }
}

/// <summary>
/// A single debit-or-credit movement within a <see cref="JournalEntry"/>.
/// Child entity, not its own aggregate root - only ever created, modified
/// or removed through the owning <see cref="JournalEntry"/>, which is what
/// lets the journal enforce "must always balance" as a single, atomic
/// invariant (Prompt 5).
/// </summary>
public sealed class JournalEntryLine : BaseEntity
{
    public Guid JournalEntryId { get; private set; }

    public int LineNumber { get; private set; }

    public Guid AccountId { get; private set; }

    public decimal DebitAmount { get; private set; }

    public decimal CreditAmount { get; private set; }

    public string? DescriptionAr { get; private set; }

    public string? DescriptionEn { get; private set; }

    private JournalEntryLine()
    {
        // Required by EF Core.
    }

    internal JournalEntryLine(Guid journalEntryId, int lineNumber, Guid accountId, decimal debitAmount, decimal creditAmount, string? descriptionAr, string? descriptionEn)
    {
        var sideRule = new JournalLineMustHaveExactlyOneSideRule(debitAmount, creditAmount);
        if (!sideRule.IsSatisfied())
        {
            throw new BusinessRuleValidationException(sideRule);
        }

        Guard.AgainstNegative(debitAmount, nameof(debitAmount));
        Guard.AgainstNegative(creditAmount, nameof(creditAmount));

        Id = Guid.NewGuid();
        JournalEntryId = journalEntryId;
        LineNumber = lineNumber;
        AccountId = Guard.AgainstEmpty(accountId, nameof(accountId));
        DebitAmount = debitAmount;
        CreditAmount = creditAmount;
        DescriptionAr = descriptionAr;
        DescriptionEn = descriptionEn;
    }

    /// <summary>Called only by the owning JournalEntry after a line is removed, to keep LineNumber values contiguous (1..N) - never called directly by application code.</summary>
    internal void Renumber(int lineNumber) => LineNumber = lineNumber;
}
