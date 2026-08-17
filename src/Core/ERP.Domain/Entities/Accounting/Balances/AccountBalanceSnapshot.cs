using ERP.Domain.Common;

namespace ERP.Domain.Entities.Accounting.Balances;

/// <summary>
/// Prompt 5 - Account Balance Rules: "Opening Balance. Debit Movement.
/// Credit Movement. Closing Balance." The immutable result of one balance
/// calculation for one <see cref="Account"/> over some caller-chosen set
/// of posted <see cref="JournalEntryLine"/>s.
///
/// Deliberately NOT an entity and NEVER persisted as its own table -
/// there is no "AccountBalances" DbSet anywhere in this solution. A stored
/// balance can silently drift out of sync with its underlying postings
/// (the single most common integrity bug in poorly-built accounting
/// systems); this snapshot is always freshly computed by
/// <see cref="AccountBalanceCalculator"/> from the actual
/// <see cref="JournalEntryLine"/> rows, which remain the single source of
/// truth. "Monthly Balance", "Yearly Balance" and "Historical Balance"
/// (Prompt 5) are not separate concepts - they are this same snapshot
/// computed over a caller-chosen date-filtered line set (a month's lines,
/// a year's lines, or lines up to an arbitrary historical date).
/// </summary>
public sealed class AccountBalanceSnapshot : ValueObject
{
    public Guid AccountId { get; }

    public decimal OpeningBalance { get; }

    public decimal TotalDebit { get; }

    public decimal TotalCredit { get; }

    public decimal ClosingBalance { get; }

    public AccountBalanceSnapshot(Guid accountId, decimal openingBalance, decimal totalDebit, decimal totalCredit, decimal closingBalance)
    {
        AccountId = accountId;
        OpeningBalance = openingBalance;
        TotalDebit = totalDebit;
        TotalCredit = totalCredit;
        ClosingBalance = closingBalance;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return AccountId;
        yield return OpeningBalance;
        yield return TotalDebit;
        yield return TotalCredit;
        yield return ClosingBalance;
    }
}

/// <summary>
/// Prompt 5 - Account Balance Rules: "Running Balance." One point in a
/// running-balance sequence: the balance immediately after one specific
/// posted line, in chronological order. A plain, dependency-free record
/// (not a <see cref="ValueObject"/>) since it is a transient calculation
/// row, never embedded in or compared against a persisted entity's state.
/// </summary>
public sealed record RunningBalancePoint(
    Guid JournalEntryLineId,
    Guid JournalEntryId,
    decimal DebitAmount,
    decimal CreditAmount,
    decimal RunningBalance);
