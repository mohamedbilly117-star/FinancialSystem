using ERP.Domain.Enums;
using ERP.Domain.Interfaces;

namespace ERP.Domain.Entities.Accounting.Rules;

/// <summary>
/// Prompt 5's foundational, non-negotiable accounting principle: "Every
/// journal must always balance" / "Every journal must satisfy double-entry
/// accounting." Evaluated against a journal's current lines before it may
/// leave <see cref="JournalStatus.Draft"/>.
/// </summary>
public sealed class JournalMustBalanceRule : IBusinessRule
{
    private readonly decimal _totalDebit;
    private readonly decimal _totalCredit;

    public JournalMustBalanceRule(decimal totalDebit, decimal totalCredit)
    {
        _totalDebit = totalDebit;
        _totalCredit = totalCredit;
    }

    public bool IsSatisfied() => _totalDebit == _totalCredit;

    public string Message =>
        $"Journal entry is not balanced: total debit ({_totalDebit:N2}) does not equal total credit ({_totalCredit:N2}).";
}

/// <summary>
/// Double-entry accounting requires at least two lines (a single-line
/// "journal entry" cannot balance two different accounts against each
/// other). Distinct from <see cref="JournalMustBalanceRule"/> because a
/// one-line journal with Debit == Credit on the very same account is
/// nonsensical even though the raw numbers technically balance.
/// </summary>
public sealed class JournalMustHaveAtLeastTwoLinesRule : IBusinessRule
{
    private readonly int _lineCount;

    public JournalMustHaveAtLeastTwoLinesRule(int lineCount) => _lineCount = lineCount;

    public bool IsSatisfied() => _lineCount >= 2;

    public string Message => $"A journal entry must have at least two lines (double-entry accounting); this entry has {_lineCount}.";
}

/// <summary>
/// Prompt 5 - Posting Rules: "Period Validation. Fiscal Year Validation."
/// A journal may only be posted into a period that is currently open for
/// posting (<see cref="AccountingPeriodStatus.Open"/> or
/// <see cref="AccountingPeriodStatus.Adjustment"/>), never into a period
/// that is Closed, Locked, or (paradoxically) merely Reopened-but-not-yet-
/// re-opened-for-this-purpose - Reopened periods must be explicitly
/// transitioned back to Open before posting resumes, keeping "reopen" an
/// auditable, deliberate two-step action rather than an implicit unlock.
/// </summary>
public sealed class FiscalPeriodMustBeOpenForPostingRule : IBusinessRule
{
    private readonly AccountingPeriodStatus _periodStatus;

    public FiscalPeriodMustBeOpenForPostingRule(AccountingPeriodStatus periodStatus) => _periodStatus = periodStatus;

    public bool IsSatisfied() => _periodStatus is AccountingPeriodStatus.Open or AccountingPeriodStatus.Adjustment;

    public string Message => $"Cannot post: the accounting period's status is '{_periodStatus}', which does not allow posting.";
}

/// <summary>
/// Prompt 5 - Account Validation: "Inactive account... Missing account."
/// Prompt 5 - Chart of Accounts Design: Parent accounts exist only to
/// summarize their children and must never receive a posting directly
/// (Prompt 5: "Posting restrictions").
/// </summary>
public sealed class AccountMustAllowPostingRule : IBusinessRule
{
    private readonly bool _isActive;
    private readonly AccountClassification _classification;
    private readonly string _accountCode;

    public AccountMustAllowPostingRule(string accountCode, bool isActive, AccountClassification classification)
    {
        _accountCode = accountCode;
        _isActive = isActive;
        _classification = classification;
    }

    public bool IsSatisfied() => _isActive && _classification != AccountClassification.Parent;

    public string Message => !_isActive
        ? $"Account '{_accountCode}' is inactive and cannot receive postings."
        : $"Account '{_accountCode}' is a Parent (summary) account and cannot receive postings directly - post to one of its posting-level children instead.";
}

/// <summary>
/// A journal entry line must represent movement on exactly one side -
/// never both Debit and Credit non-zero on the same line, and never both
/// zero (a line that moves nothing is not a valid accounting entry).
/// </summary>
public sealed class JournalLineMustHaveExactlyOneSideRule : IBusinessRule
{
    private readonly decimal _debit;
    private readonly decimal _credit;

    public JournalLineMustHaveExactlyOneSideRule(decimal debit, decimal credit)
    {
        _debit = debit;
        _credit = credit;
    }

    public bool IsSatisfied() => (_debit > 0) ^ (_credit > 0);

    public string Message =>
        "A journal entry line must have either a debit amount or a credit amount (not both, and not neither).";
}
