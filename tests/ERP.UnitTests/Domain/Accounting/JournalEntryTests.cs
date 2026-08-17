using ERP.Domain.Entities.Accounting;
using ERP.Domain.Enums;
using ERP.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace ERP.UnitTests.Domain.Accounting;

public class JournalEntryTests
{
    private static Account CreatePostingAccount(string code, AccountType type = AccountType.Asset, AccountNormalBalance normalBalance = AccountNormalBalance.Debit)
        => Account.CreateRoot(code, $"حساب {code}", $"Account {code}", type, normalBalance, AccountClassification.Posting);

    private static JournalEntry CreateDraftEntry()
        => JournalEntry.CreateDraft(
            fiscalYearId: Guid.NewGuid(),
            accountingPeriodId: Guid.NewGuid(),
            entryDate: new DateOnly(2026, 7, 1),
            descriptionAr: "قيد اختبار",
            descriptionEn: "Test entry");

    [Fact]
    public void AddLine_WithBothDebitAndCreditZero_Throws()
    {
        var entry = CreateDraftEntry();
        var account = CreatePostingAccount("1101");

        Action act = () => entry.AddLine(account, debitAmount: 0, creditAmount: 0);

        act.Should().Throw<BusinessRuleValidationException>();
    }

    [Fact]
    public void AddLine_WithBothDebitAndCreditNonZero_Throws()
    {
        var entry = CreateDraftEntry();
        var account = CreatePostingAccount("1101");

        Action act = () => entry.AddLine(account, debitAmount: 100, creditAmount: 50);

        act.Should().Throw<BusinessRuleValidationException>();
    }

    [Fact]
    public void AddLine_ToParentAccount_Throws()
    {
        var entry = CreateDraftEntry();
        var parentAccount = Account.CreateRoot("1000", "الأصول", "Assets", AccountType.Asset, AccountNormalBalance.Debit, AccountClassification.Parent);

        Action act = () => entry.AddLine(parentAccount, debitAmount: 100, creditAmount: 0);

        act.Should().Throw<BusinessRuleValidationException>()
            .WithMessage("*Parent*");
    }

    [Fact]
    public void AddLine_ToInactiveAccount_Throws()
    {
        var entry = CreateDraftEntry();
        var account = CreatePostingAccount("1101");
        account.Deactivate();

        Action act = () => entry.AddLine(account, debitAmount: 100, creditAmount: 0);

        act.Should().Throw<BusinessRuleValidationException>()
            .WithMessage("*inactive*");
    }

    [Fact]
    public void Submit_WithOnlyOneLine_Throws()
    {
        var entry = CreateDraftEntry();
        var cash = CreatePostingAccount("1101");
        entry.AddLine(cash, debitAmount: 100, creditAmount: 0);

        Action act = () => entry.Submit();

        act.Should().Throw<BusinessRuleValidationException>()
            .WithMessage("*at least two lines*");
    }

    [Fact]
    public void Submit_WithUnbalancedLines_Throws()
    {
        var entry = CreateDraftEntry();
        var cash = CreatePostingAccount("1101");
        var revenue = CreatePostingAccount("4101", AccountType.Revenue, AccountNormalBalance.Credit);

        entry.AddLine(cash, debitAmount: 100, creditAmount: 0);
        entry.AddLine(revenue, debitAmount: 0, creditAmount: 90); // deliberately unbalanced

        Action act = () => entry.Submit();

        act.Should().Throw<BusinessRuleValidationException>()
            .WithMessage("*not balanced*");
    }

    [Fact]
    public void FullLifecycle_Draft_Submit_Approve_Post_Succeeds()
    {
        var entry = CreateDraftEntry();
        var cash = CreatePostingAccount("1101");
        var revenue = CreatePostingAccount("4101", AccountType.Revenue, AccountNormalBalance.Credit);

        entry.AddLine(cash, debitAmount: 500, creditAmount: 0);
        entry.AddLine(revenue, debitAmount: 0, creditAmount: 500);

        entry.Submit();
        entry.Status.Should().Be(JournalStatus.PendingApproval);

        var approver = Guid.NewGuid();
        entry.Approve(approver, DateTime.UtcNow);
        entry.Status.Should().Be(JournalStatus.Approved);

        entry.Post(Guid.NewGuid(), DateTime.UtcNow, "JV-2026-000001", AccountingPeriodStatus.Open);

        entry.Status.Should().Be(JournalStatus.Posted);
        entry.JournalNumber.Should().Be("JV-2026-000001");
        entry.TotalDebit.Should().Be(entry.TotalCredit);
    }

    [Fact]
    public void Post_DirectlyFromDraft_SupportsAutomaticPostingPath()
    {
        // Prompt 5 explicitly requires BOTH "Automatic Posting" and "Manual
        // Approval Before Posting" to be supported - this covers the
        // automatic path, which must not require Submit()/Approve() first.
        var entry = CreateDraftEntry();
        var cash = CreatePostingAccount("1101");
        var revenue = CreatePostingAccount("4101", AccountType.Revenue, AccountNormalBalance.Credit);

        entry.AddLine(cash, debitAmount: 250, creditAmount: 0);
        entry.AddLine(revenue, debitAmount: 0, creditAmount: 250);

        entry.Post(Guid.NewGuid(), DateTime.UtcNow, "JV-2026-000002", AccountingPeriodStatus.Open);

        entry.Status.Should().Be(JournalStatus.Posted);
    }

    [Fact]
    public void Post_WhenPeriodNotOpen_Throws()
    {
        var entry = CreateDraftEntry();
        var cash = CreatePostingAccount("1101");
        var revenue = CreatePostingAccount("4101", AccountType.Revenue, AccountNormalBalance.Credit);
        entry.AddLine(cash, debitAmount: 100, creditAmount: 0);
        entry.AddLine(revenue, debitAmount: 0, creditAmount: 100);

        Action act = () => entry.Post(Guid.NewGuid(), DateTime.UtcNow, "JV-2026-000003", AccountingPeriodStatus.Closed);

        act.Should().Throw<BusinessRuleValidationException>()
            .WithMessage("*does not allow posting*");
    }

    [Fact]
    public void CreateReversal_ProducesBalancedMirrorWithSwappedSides()
    {
        var entry = CreateDraftEntry();
        var cash = CreatePostingAccount("1101");
        var revenue = CreatePostingAccount("4101", AccountType.Revenue, AccountNormalBalance.Credit);
        entry.AddLine(cash, debitAmount: 300, creditAmount: 0);
        entry.AddLine(revenue, debitAmount: 0, creditAmount: 300);
        entry.Post(Guid.NewGuid(), DateTime.UtcNow, "JV-2026-000004", AccountingPeriodStatus.Open);

        var reversal = entry.CreateReversal("Data entry error", Guid.NewGuid(), new DateOnly(2026, 7, 2));

        entry.Status.Should().Be(JournalStatus.Reversed);
        reversal.Status.Should().Be(JournalStatus.Draft);
        reversal.Lines.Should().HaveCount(2);
        reversal.TotalDebit.Should().Be(300);
        reversal.TotalCredit.Should().Be(300);
        reversal.Lines.Should().Contain(l => l.AccountId == cash.Id && l.CreditAmount == 300 && l.DebitAmount == 0);
        reversal.Lines.Should().Contain(l => l.AccountId == revenue.Id && l.DebitAmount == 300 && l.CreditAmount == 0);
    }

    [Fact]
    public void CreateReversal_WhenNotPosted_Throws()
    {
        var entry = CreateDraftEntry();

        Action act = () => entry.CreateReversal("reason", Guid.NewGuid(), new DateOnly(2026, 7, 2));

        act.Should().Throw<DomainException>()
            .WithMessage("*Posted*");
    }

    [Fact]
    public void RemoveLine_AfterSubmit_Throws()
    {
        var entry = CreateDraftEntry();
        var cash = CreatePostingAccount("1101");
        var revenue = CreatePostingAccount("4101", AccountType.Revenue, AccountNormalBalance.Credit);
        entry.AddLine(cash, debitAmount: 100, creditAmount: 0);
        var lineToRemove = entry.AddLine(revenue, debitAmount: 0, creditAmount: 100);
        entry.Submit();

        Action act = () => entry.RemoveLine(lineToRemove.Id);

        act.Should().Throw<DomainException>()
            .WithMessage("*Draft*");
    }
}
