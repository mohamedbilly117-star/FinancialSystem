using ERP.Domain.Entities.Accounting;
using ERP.Domain.Entities.Accounting.Balances;
using ERP.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace ERP.UnitTests.Domain.Balances;

public class AccountBalanceCalculatorTests
{
    private static Account CreateDebitNormalAccount(string code = "1101")
        => Account.CreateRoot(code, $"حساب {code}", $"Account {code}", AccountType.Asset, AccountNormalBalance.Debit, AccountClassification.Posting);

    private static Account CreateCreditNormalAccount(string code = "4101")
        => Account.CreateRoot(code, $"حساب {code}", $"Account {code}", AccountType.Revenue, AccountNormalBalance.Credit, AccountClassification.Posting);

    /// <summary>
    /// Builds a set of JournalEntryLines targeting <paramref name="account"/>
    /// with the given (debit, credit) pairs. Goes through the real
    /// JournalEntry.AddLine API (JournalEntryLine's constructor is
    /// internal, matching the existing JournalEntryTests.cs convention) -
    /// the resulting JournalEntry is never Submitted/Posted, since these
    /// tests exercise AccountBalanceCalculator in isolation and do not
    /// need (or want) journal-level balance validation to apply.
    /// </summary>
    private static IReadOnlyList<JournalEntryLine> CreateLines(Account account, params (decimal Debit, decimal Credit)[] amounts)
    {
        var journal = JournalEntry.CreateDraft(Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 1, 1), "قيد اختبار", "Test entry");

        foreach (var (debit, credit) in amounts)
        {
            journal.AddLine(account, debit, credit);
        }

        return journal.Lines.Where(l => l.AccountId == account.Id).ToList();
    }

    [Fact]
    public void Calculate_DebitNormalAccount_DebitIncreasesCreditDecreasesBalance()
    {
        var account = CreateDebitNormalAccount();
        var lines = CreateLines(account, (500m, 0m), (0m, 200m));

        var result = AccountBalanceCalculator.Calculate(account, 1000m, lines);

        result.OpeningBalance.Should().Be(1000m);
        result.TotalDebit.Should().Be(500m);
        result.TotalCredit.Should().Be(200m);
        result.ClosingBalance.Should().Be(1300m); // 1000 + 500 - 200
    }

    [Fact]
    public void Calculate_CreditNormalAccount_CreditIncreasesDebitDecreasesBalance()
    {
        var account = CreateCreditNormalAccount();
        var lines = CreateLines(account, (200m, 0m), (0m, 500m));

        var result = AccountBalanceCalculator.Calculate(account, 1000m, lines);

        result.TotalDebit.Should().Be(200m);
        result.TotalCredit.Should().Be(500m);
        result.ClosingBalance.Should().Be(1300m); // 1000 + 500 - 200 (credit side increases here)
    }

    [Fact]
    public void Calculate_SameRawMovement_ProducesOppositeEffectDependingOnAccountNature()
    {
        // The exact same Debit=500/Credit=200 movement means opposite
        // things depending on which kind of account it happened to.
        var debitNormal = CreateDebitNormalAccount("1101");
        var creditNormal = CreateCreditNormalAccount("4101");
        var debitNormalLines = CreateLines(debitNormal, (500m, 0m), (0m, 200m));
        var creditNormalLines = CreateLines(creditNormal, (500m, 0m), (0m, 200m));

        var debitNormalResult = AccountBalanceCalculator.Calculate(debitNormal, 0m, debitNormalLines);
        var creditNormalResult = AccountBalanceCalculator.Calculate(creditNormal, 0m, creditNormalLines);

        debitNormalResult.ClosingBalance.Should().Be(300m);   // 0 + 500 - 200
        creditNormalResult.ClosingBalance.Should().Be(-300m); // 0 + 200 - 500
    }

    [Fact]
    public void Calculate_WithNoLines_ReturnsOpeningBalanceUnchangedAsClosingBalance()
    {
        var account = CreateDebitNormalAccount();

        var result = AccountBalanceCalculator.Calculate(account, 750m, Array.Empty<JournalEntryLine>());

        result.TotalDebit.Should().Be(0m);
        result.TotalCredit.Should().Be(0m);
        result.ClosingBalance.Should().Be(750m);
    }

    [Fact]
    public void Calculate_ZeroOpeningBalanceWithEqualDebitAndCredit_ReturnsZeroClosingBalance()
    {
        var account = CreateDebitNormalAccount();
        var lines = CreateLines(account, (1000m, 0m), (0m, 1000m));

        var result = AccountBalanceCalculator.Calculate(account, 0m, lines);

        result.ClosingBalance.Should().Be(0m);
    }

    [Fact]
    public void Calculate_NegativeOpeningBalance_IsCarriedThroughCorrectly()
    {
        // A negative opening balance is a legitimate edge case (e.g. an
        // overdrawn account, or a Liability account's "normal" positive
        // balance expressed on the Debit-normal side of a report) - the
        // calculator must not reject or clamp it, only compute against it.
        var account = CreateDebitNormalAccount();
        var lines = CreateLines(account, (300m, 0m));

        var result = AccountBalanceCalculator.Calculate(account, -500m, lines);

        result.ClosingBalance.Should().Be(-200m); // -500 + 300 - 0
    }

    [Fact]
    public void Calculate_WithNullAccount_Throws()
    {
        Action act = () => AccountBalanceCalculator.Calculate(null!, 0m, Array.Empty<JournalEntryLine>());

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Calculate_WithNullLines_Throws()
    {
        var account = CreateDebitNormalAccount();

        Action act = () => AccountBalanceCalculator.Calculate(account, 0m, null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CalculateRunningBalances_DebitNormalAccount_AccumulatesInOrder()
    {
        var account = CreateDebitNormalAccount();
        var lines = CreateLines(account, (500m, 0m), (0m, 200m), (300m, 0m));

        var points = AccountBalanceCalculator.CalculateRunningBalances(account, 1000m, lines);

        points.Should().HaveCount(3);
        points[0].RunningBalance.Should().Be(1500m); // 1000 + 500
        points[1].RunningBalance.Should().Be(1300m); // 1500 - 200
        points[2].RunningBalance.Should().Be(1600m); // 1300 + 300
    }

    [Fact]
    public void CalculateRunningBalances_CreditNormalAccount_AccumulatesInOrder()
    {
        var account = CreateCreditNormalAccount();
        var lines = CreateLines(account, (0m, 500m), (200m, 0m), (0m, 300m));

        var points = AccountBalanceCalculator.CalculateRunningBalances(account, 1000m, lines);

        points.Should().HaveCount(3);
        points[0].RunningBalance.Should().Be(1500m); // 1000 + 500 (credit increases)
        points[1].RunningBalance.Should().Be(1300m); // 1500 - 200 (debit decreases)
        points[2].RunningBalance.Should().Be(1600m); // 1300 + 300
    }

    [Fact]
    public void CalculateRunningBalances_EachPointReferencesItsOwnLine()
    {
        var account = CreateDebitNormalAccount();
        var lines = CreateLines(account, (500m, 0m), (0m, 200m));

        var points = AccountBalanceCalculator.CalculateRunningBalances(account, 0m, lines);

        points[0].JournalEntryLineId.Should().Be(lines[0].Id);
        points[0].DebitAmount.Should().Be(500m);
        points[1].JournalEntryLineId.Should().Be(lines[1].Id);
        points[1].CreditAmount.Should().Be(200m);
    }

    [Fact]
    public void CalculateRunningBalances_WithNoLines_ReturnsEmptyList()
    {
        var account = CreateDebitNormalAccount();

        var points = AccountBalanceCalculator.CalculateRunningBalances(account, 500m, Array.Empty<JournalEntryLine>());

        points.Should().BeEmpty();
    }

    [Fact]
    public void CalculateRunningBalances_WithNullAccount_Throws()
    {
        Action act = () => AccountBalanceCalculator.CalculateRunningBalances(null!, 0m, Array.Empty<JournalEntryLine>());

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AccountBalanceSnapshot_TwoSnapshotsWithIdenticalComponents_AreEqual()
    {
        var accountId = Guid.NewGuid();
        var a = new AccountBalanceSnapshot(accountId, 100m, 50m, 20m, 130m);
        var b = new AccountBalanceSnapshot(accountId, 100m, 50m, 20m, 130m);

        a.Should().Be(b);
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void AccountBalanceSnapshot_SnapshotsWithDifferentClosingBalance_AreNotEqual()
    {
        var accountId = Guid.NewGuid();
        var a = new AccountBalanceSnapshot(accountId, 100m, 50m, 20m, 130m);
        var b = new AccountBalanceSnapshot(accountId, 100m, 50m, 20m, 999m);

        a.Should().NotBe(b);
        (a == b).Should().BeFalse();
    }
}
