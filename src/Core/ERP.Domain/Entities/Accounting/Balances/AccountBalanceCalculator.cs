using ERP.Domain.Enums;
using ERP.Shared.Guards;

namespace ERP.Domain.Entities.Accounting.Balances;

using ERP.Domain.Entities.Accounting; // Account, JournalEntryLine: this file's namespace is nested under ERP.Domain.Entities.Accounting, which is not automatically visible without this explicit using (same pattern as Distribution/ and RuleEngine/).

/// <summary>
/// Prompt 5 - Account Balance Rules: "Opening Balance. Debit Movement.
/// Credit Movement. Closing Balance. Running Balance. Monthly Balance.
/// Yearly Balance. Historical Balance."
///
/// A pure, stateless Domain Service - no database access, no dependency
/// on <see cref="JournalEntry"/>'s lifecycle state. It trusts the caller
/// to have already selected the right set of lines (e.g. "every line from
/// every currently-Posted JournalEntry for this account, dated within
/// fiscal period X" - an Application-layer repository query), exactly the
/// same division of responsibility already established by
/// <see cref="RuleEngine.AccountingRuleResolver"/> and
/// <see cref="Distribution.DistributionTemplate"/>'s "automatic
/// selection" note: assembling candidates needs the database; deciding
/// what they mean given the account's nature does not, and is therefore
/// kept here, fully unit-testable in isolation.
///
/// "Monthly Balance", "Yearly Balance" and "Historical Balance" are not
/// separate methods - they are exactly the same calculation, called with
/// a line set the caller has already date-filtered to a month, a year, or
/// up to an arbitrary historical date, respectively.
/// </summary>
public static class AccountBalanceCalculator
{
    /// <summary>
    /// Computes total movement and closing balance for
    /// <paramref name="account"/> over <paramref name="postedLines"/>.
    /// Correctly interprets debit vs. credit movement according to the
    /// account's <see cref="Account.NormalBalance"/> (Prompt 5: an Asset/
    /// Expense account's balance INCREASES on the debit side, while a
    /// Liability/Equity/Revenue account's balance INCREASES on the credit
    /// side - the identical raw Debit/Credit totals mean opposite things
    /// for the closing balance depending on which kind of account they
    /// belong to).
    /// </summary>
    /// <param name="account">The account being summarized. Its <see cref="Account.NormalBalance"/> determines which side increases the balance.</param>
    /// <param name="openingBalance">The balance carried in from before the first line in <paramref name="postedLines"/> - zero for a brand-new account, or a prior period's closing balance when computing a later period's balance.</param>
    /// <param name="postedLines">Lines to summarize - the caller is responsible for restricting this to lines belonging to Posted journal entries and to the desired date range; this method does not (and cannot, without a database) verify that itself.</param>
    public static AccountBalanceSnapshot Calculate(Account account, decimal openingBalance, IEnumerable<JournalEntryLine> postedLines)
    {
        Guard.AgainstNull(account, nameof(account));
        Guard.AgainstNull(postedLines, nameof(postedLines));

        var totalDebit = 0m;
        var totalCredit = 0m;

        foreach (var line in postedLines)
        {
            totalDebit += line.DebitAmount;
            totalCredit += line.CreditAmount;
        }

        var closingBalance = ApplyMovement(account.NormalBalance, openingBalance, totalDebit, totalCredit);

        return new AccountBalanceSnapshot(account.Id, openingBalance, totalDebit, totalCredit, closingBalance);
    }

    /// <summary>
    /// Prompt 5 - "Running Balance": the balance immediately after each
    /// line, in the order <paramref name="chronologicallyOrderedPostedLines"/>
    /// is supplied. The caller is responsible for supplying the lines in
    /// the correct chronological (or posting) order - this method does
    /// not re-sort them, since "correct order" depends on context
    /// (transaction date vs. posting date vs. entry sequence) that only
    /// the caller's query knows.
    /// </summary>
    public static IReadOnlyList<RunningBalancePoint> CalculateRunningBalances(Account account, decimal openingBalance, IEnumerable<JournalEntryLine> chronologicallyOrderedPostedLines)
    {
        Guard.AgainstNull(account, nameof(account));
        Guard.AgainstNull(chronologicallyOrderedPostedLines, nameof(chronologicallyOrderedPostedLines));

        var points = new List<RunningBalancePoint>();
        var runningBalance = openingBalance;

        foreach (var line in chronologicallyOrderedPostedLines)
        {
            runningBalance = ApplyMovement(account.NormalBalance, runningBalance, line.DebitAmount, line.CreditAmount);
            points.Add(new RunningBalancePoint(line.Id, line.JournalEntryId, line.DebitAmount, line.CreditAmount, runningBalance));
        }

        return points;
    }

    /// <summary>
    /// The one place the debit/credit-direction interpretation rule
    /// lives, shared by both <see cref="Calculate"/> and
    /// <see cref="CalculateRunningBalances"/> so the two can never drift
    /// out of agreement with each other.
    /// </summary>
    private static decimal ApplyMovement(AccountNormalBalance normalBalance, decimal startingBalance, decimal debit, decimal credit)
        => normalBalance == AccountNormalBalance.Debit
            ? startingBalance + debit - credit
            : startingBalance + credit - debit;
}
