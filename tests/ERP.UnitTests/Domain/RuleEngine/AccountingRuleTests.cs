using ERP.Domain.Entities.Accounting;
using ERP.Domain.Entities.Accounting.RuleEngine;
using ERP.Domain.Enums;
using ERP.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace ERP.UnitTests.Domain.RuleEngine;

public class AccountingRuleTests
{
    private static Account CreateAccount(string code, AccountType type = AccountType.Asset, AccountNormalBalance normalBalance = AccountNormalBalance.Debit, AccountClassification classification = AccountClassification.Posting)
        => Account.CreateRoot(code, $"حساب {code}", $"Account {code}", type, normalBalance, classification);

    private static Account CashAccount() => CreateAccount("1101");

    private static Account RevenueAccount() => CreateAccount("4101", AccountType.Revenue, AccountNormalBalance.Credit);

    [Fact]
    public void CreateFirstVersion_WithFixedDebitAndCreditAccounts_Succeeds()
    {
        var debit = CashAccount();
        var credit = RevenueAccount();

        var rule = AccountingRule.CreateFirstVersion(
            "REVENUE_COLLECTION",
            "RC-GENERAL",
            "التحصيل العام",
            "General Collection",
            100,
            false,
            debit,
            null,
            credit,
            null,
            false,
            new DateOnly(2026, 1, 1));

        rule.DebitAccountId.Should().Be(debit.Id);
        rule.CreditAccountId.Should().Be(credit.Id);
        rule.DebitDistributionSourceType.Should().BeNull();
        rule.CreditDistributionSourceType.Should().BeNull();
        rule.IsActive.Should().BeFalse();
        rule.Version.Should().Be(1);
    }

    [Fact]
    public void CreateFirstVersion_WithBothDebitAccountAndDistributionSourceType_Throws()
    {
        var debit = CashAccount();
        var credit = RevenueAccount();

        Action act = () => AccountingRule.CreateFirstVersion(
            "REVENUE_COLLECTION", "RC-BAD", "قاعدة", "Rule", 100, false,
            debit, DistributionSourceType.RevenueCategory,
            credit, null,
            false, new DateOnly(2026, 1, 1));

        act.Should().Throw<BusinessRuleValidationException>()
            .WithMessage("*cannot be both*");
    }

    [Fact]
    public void CreateFirstVersion_WithNeitherDebitAccountNorDistributionSourceType_Throws()
    {
        var credit = RevenueAccount();

        Action act = () => AccountingRule.CreateFirstVersion(
            "REVENUE_COLLECTION", "RC-BAD", "قاعدة", "Rule", 100, false,
            null, null,
            credit, null,
            false, new DateOnly(2026, 1, 1));

        act.Should().Throw<BusinessRuleValidationException>()
            .WithMessage("*neither was specified*");
    }

    [Fact]
    public void CreateFirstVersion_WithSameAccountForDebitAndCredit_Throws()
    {
        var account = CashAccount();

        Action act = () => AccountingRule.CreateFirstVersion(
            "REVENUE_COLLECTION", "RC-BAD", "قاعدة", "Rule", 100, false,
            account, null,
            account, null,
            false, new DateOnly(2026, 1, 1));

        act.Should().Throw<BusinessRuleValidationException>()
            .WithMessage("*must not be the same account*");
    }

    [Fact]
    public void CreateFirstVersion_ToParentAccount_Throws()
    {
        var parentAccount = Account.CreateRoot("1000", "الأصول", "Assets", AccountType.Asset, AccountNormalBalance.Debit, AccountClassification.Parent);
        var credit = RevenueAccount();

        Action act = () => AccountingRule.CreateFirstVersion(
            "REVENUE_COLLECTION", "RC-BAD", "قاعدة", "Rule", 100, false,
            parentAccount, null,
            credit, null,
            false, new DateOnly(2026, 1, 1));

        act.Should().Throw<BusinessRuleValidationException>()
            .WithMessage("*Parent*");
    }

    [Fact]
    public void CreateFirstVersion_WithDistributionDelegationForCreditSide_Succeeds()
    {
        // Prompt 5: "Distribution logic" - Debit is a fixed Bank/Cash
        // account, Credit is delegated to the Distribution Engine for
        // whichever Revenue Category the actual transaction belongs to.
        var debit = CashAccount();

        var rule = AccountingRule.CreateFirstVersion(
            "REVENUE_COLLECTION",
            "RC-DISTRIBUTED",
            "تحصيل موزع",
            "Distributed Collection",
            50,
            false,
            debit,
            null,
            null,
            DistributionSourceType.RevenueCategory,
            false,
            new DateOnly(2026, 1, 1));

        rule.DebitAccountId.Should().Be(debit.Id);
        rule.CreditAccountId.Should().BeNull();
        rule.CreditDistributionSourceType.Should().Be(DistributionSourceType.RevenueCategory);
    }

    [Fact]
    public void AddMatchCondition_BetweenOperatorMissingValueTo_Throws()
    {
        var rule = CreateSimpleFixedRule();

        Action act = () => rule.AddMatchCondition("Amount", AccountingConditionOperator.Between, "1000");

        act.Should().Throw<BusinessRuleValidationException>()
            .WithMessage("*Between*");
    }

    [Fact]
    public void AddMatchCondition_GreaterThanWithNonNumericValue_Throws()
    {
        var rule = CreateSimpleFixedRule();

        Action act = () => rule.AddMatchCondition("Amount", AccountingConditionOperator.GreaterThan, "not-a-number");

        act.Should().Throw<BusinessRuleValidationException>()
            .WithMessage("*parseable as a number*");
    }

    [Fact]
    public void AddMatchCondition_ValidEqualsCondition_Succeeds()
    {
        var rule = CreateSimpleFixedRule();

        var condition = rule.AddMatchCondition("OfficeCode", AccountingConditionOperator.Equals, "BANK-01");

        rule.MatchConditions.Should().ContainSingle().Which.Should().Be(condition);
        rule.Exceptions.Should().BeEmpty();
    }

    [Fact]
    public void AddExceptionCondition_ValidCondition_AppearsInExceptionsNotMatchConditions()
    {
        var rule = CreateSimpleFixedRule();

        rule.AddExceptionCondition("Amount", AccountingConditionOperator.GreaterThan, "50000");

        rule.Exceptions.Should().HaveCount(1);
        rule.MatchConditions.Should().BeEmpty();
    }

    [Fact]
    public void AddCondition_AfterActivation_Throws()
    {
        var rule = CreateSimpleFixedRule();
        rule.Activate();

        Action act = () => rule.AddMatchCondition("Amount", AccountingConditionOperator.GreaterThan, "100");

        act.Should().Throw<DomainException>()
            .WithMessage("*Deactivated*");
    }

    [Fact]
    public void Deactivate_ThenAddCondition_Succeeds()
    {
        var rule = CreateSimpleFixedRule();
        rule.Activate();
        rule.Deactivate();

        Action act = () => rule.AddMatchCondition("Amount", AccountingConditionOperator.GreaterThan, "100");

        act.Should().NotThrow();
    }

    [Fact]
    public void RequiresApprovalBeforePosting_IsStoredAsConfigured()
    {
        var debit = CashAccount();
        var credit = RevenueAccount();

        var rule = AccountingRule.CreateFirstVersion(
            "EXPENSE_PAYMENT", "EP-01", "دفع", "Payment", 100, false,
            debit, null, credit, null,
            true,
            new DateOnly(2026, 1, 1));

        rule.RequiresApprovalBeforePosting.Should().BeTrue();
    }

    [Fact]
    public void CreateNewVersion_WhileNotActive_Throws()
    {
        var rule = CreateSimpleFixedRule();

        Action act = () => rule.CreateNewVersion("RC-01", "قاعدة", "Rule", 100, false, CashAccount(), null, RevenueAccount(), null, false, new DateOnly(2026, 6, 1));

        act.Should().Throw<DomainException>()
            .WithMessage("*Active*");
    }

    [Fact]
    public void CreateNewVersion_HappyPath_ProducesVersionTwoAndClosesOldEffectiveRange()
    {
        var v1 = CreateSimpleFixedRule();
        v1.Activate();

        var newDebit = CashAccount();
        var newCredit = RevenueAccount();
        var v2 = v1.CreateNewVersion("RC-01", "قاعدة معدلة", "Revised Rule", 90, false, newDebit, null, newCredit, null, true, new DateOnly(2026, 6, 1));

        v2.Version.Should().Be(2);
        v2.IsActive.Should().BeFalse();
        v2.SourceModuleCode.Should().Be(v1.SourceModuleCode);
        v2.RequiresApprovalBeforePosting.Should().BeTrue();

        v1.EffectiveTo.Should().Be(new DateOnly(2026, 5, 31));
        v1.IsActive.Should().BeTrue();
    }

    private static AccountingRule CreateSimpleFixedRule()
        => AccountingRule.CreateFirstVersion(
            "REVENUE_COLLECTION", "RC-01", "قاعدة", "Rule", 100, false,
            CashAccount(), null, RevenueAccount(), null,
            false, new DateOnly(2026, 1, 1));
}
