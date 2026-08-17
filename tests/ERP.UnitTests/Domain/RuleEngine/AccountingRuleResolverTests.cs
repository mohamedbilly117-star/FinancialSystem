using ERP.Domain.Entities.Accounting;
using ERP.Domain.Entities.Accounting.RuleEngine;
using ERP.Domain.Enums;
using ERP.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace ERP.UnitTests.Domain.RuleEngine;

public class AccountingRuleResolverTests
{
    private const string Module = "REVENUE_COLLECTION";
    private static readonly DateOnly AsOfDate = new(2026, 7, 15);

    private static Account CreateAccount(string code, AccountType type = AccountType.Asset, AccountNormalBalance normalBalance = AccountNormalBalance.Debit)
        => Account.CreateRoot(code, $"حساب {code}", $"Account {code}", type, normalBalance, AccountClassification.Posting);

    private static AccountingRule CreateActiveRule(
        string code,
        int priority,
        string sourceModuleCode = Module,
        DateOnly? effectiveFrom = null,
        DateOnly? effectiveTo = null)
    {
        var debit = CreateAccount("1101-" + code);
        var credit = CreateAccount("4101-" + code, AccountType.Revenue, AccountNormalBalance.Credit);

        var rule = AccountingRule.CreateFirstVersion(
            sourceModuleCode, code, code, code, priority, false,
            debit, null, credit, null,
            false,
            effectiveFrom ?? new DateOnly(2026, 1, 1));

        rule.Activate();

        if (effectiveTo is not null)
        {
            SetEffectiveToForTesting(rule, effectiveTo.Value);
        }

        return rule;
    }

    /// <summary>
    /// EffectiveTo is only ever set by CreateNewVersion in normal
    /// operation (there is deliberately no public "close this version's
    /// window" method on its own). To test the EffectiveTo boundary in
    /// isolation without going through a full CreateNewVersion, drive it
    /// via the same public API a real caller would use: supersede the rule
    /// with a new version starting the day after the desired cutoff, which
    /// closes THIS instance's EffectiveTo as a side effect.
    /// </summary>
    private static void SetEffectiveToForTesting(AccountingRule rule, DateOnly effectiveTo)
    {
        var newDebit = CreateAccount("1101-NEW-" + rule.Code);
        var newCredit = CreateAccount("4101-NEW-" + rule.Code, AccountType.Revenue, AccountNormalBalance.Credit);
        rule.CreateNewVersion(rule.Code, rule.NameAr, rule.NameEn, rule.Priority, rule.IsException, newDebit, null, newCredit, null, rule.RequiresApprovalBeforePosting, effectiveTo.AddDays(1));
    }

    [Fact]
    public void Resolve_NoCandidates_ReturnsNull()
    {
        var result = AccountingRuleResolver.Resolve(Array.Empty<AccountingRule>(), Module, new Dictionary<string, string>(), AsOfDate);

        result.Should().BeNull();
    }

    [Fact]
    public void Resolve_SingleUnconditionalActiveRule_ReturnsIt()
    {
        var rule = CreateActiveRule("R1", priority: 100);

        var result = AccountingRuleResolver.Resolve(new[] { rule }, Module, new Dictionary<string, string>(), AsOfDate);

        result.Should().Be(rule);
    }

    [Fact]
    public void Resolve_ExcludesInactiveRules()
    {
        var debit = CreateAccount("1101");
        var credit = CreateAccount("4101", AccountType.Revenue, AccountNormalBalance.Credit);
        var inactiveRule = AccountingRule.CreateFirstVersion(
            Module, "INACTIVE", "غير مفعل", "Inactive", 100, false,
            debit, null, credit, null,
            false, new DateOnly(2026, 1, 1));
        // Deliberately never call Activate().

        var result = AccountingRuleResolver.Resolve(new[] { inactiveRule }, Module, new Dictionary<string, string>(), AsOfDate);

        result.Should().BeNull();
    }

    [Fact]
    public void Resolve_ExcludesRuleNotYetEffective()
    {
        var futureRule = CreateActiveRule("FUTURE", priority: 100, effectiveFrom: AsOfDate.AddDays(1));

        var result = AccountingRuleResolver.Resolve(new[] { futureRule }, Module, new Dictionary<string, string>(), AsOfDate);

        result.Should().BeNull();
    }

    [Fact]
    public void Resolve_ExcludesRuleAfterItsEffectiveToDate()
    {
        var expiredRule = CreateActiveRule("EXPIRED", priority: 100, effectiveFrom: new DateOnly(2026, 1, 1), effectiveTo: new DateOnly(2026, 6, 30));

        // AsOfDate (2026-07-15) is after EXPIRED's EffectiveTo (2026-06-30).
        var result = AccountingRuleResolver.Resolve(new[] { expiredRule }, Module, new Dictionary<string, string>(), AsOfDate);

        result.Should().BeNull();
    }

    [Fact]
    public void Resolve_IncludesRuleWithinItsEffectiveWindow()
    {
        var rule = CreateActiveRule("CURRENT", priority: 100, effectiveFrom: new DateOnly(2026, 1, 1), effectiveTo: new DateOnly(2026, 12, 31));

        var result = AccountingRuleResolver.Resolve(new[] { rule }, Module, new Dictionary<string, string>(), AsOfDate);

        result.Should().Be(rule);
    }

    [Fact]
    public void Resolve_PicksLowerPriorityValueOverHigher()
    {
        var lowPrecedence = CreateActiveRule("LOW", priority: 200);
        var highPrecedence = CreateActiveRule("HIGH", priority: 10);

        var result = AccountingRuleResolver.Resolve(new[] { lowPrecedence, highPrecedence }, Module, new Dictionary<string, string>(), AsOfDate);

        result.Should().Be(highPrecedence);
    }

    [Fact]
    public void Resolve_ExcludesRuleWhenMatchConditionNotSatisfied()
    {
        var rule = CreateActiveRule("COND", priority: 100);
        rule.Deactivate();
        rule.AddMatchCondition("OfficeCode", AccountingConditionOperator.Equals, "BANK-01");
        rule.Activate();

        var context = new Dictionary<string, string> { ["OfficeCode"] = "BANK-02" };
        var result = AccountingRuleResolver.Resolve(new[] { rule }, Module, context, AsOfDate);

        result.Should().BeNull();
    }

    [Fact]
    public void Resolve_IncludesRuleWhenMatchConditionSatisfied()
    {
        var rule = CreateActiveRule("COND", priority: 100);
        rule.Deactivate();
        rule.AddMatchCondition("OfficeCode", AccountingConditionOperator.Equals, "BANK-01");
        rule.Activate();

        var context = new Dictionary<string, string> { ["OfficeCode"] = "BANK-01" };
        var result = AccountingRuleResolver.Resolve(new[] { rule }, Module, context, AsOfDate);

        result.Should().Be(rule);
    }

    [Fact]
    public void Resolve_ExcludesRuleWhenExceptionConditionSatisfied()
    {
        // "Applies to everything EXCEPT transactions from SPECIAL-OFFICE."
        var rule = CreateActiveRule("GENERAL", priority: 100);
        rule.Deactivate();
        rule.AddExceptionCondition("OfficeCode", AccountingConditionOperator.Equals, "SPECIAL-OFFICE");
        rule.Activate();

        var context = new Dictionary<string, string> { ["OfficeCode"] = "SPECIAL-OFFICE" };
        var result = AccountingRuleResolver.Resolve(new[] { rule }, Module, context, AsOfDate);

        result.Should().BeNull();
    }

    [Fact]
    public void Resolve_AppliesRuleWhenExceptionConditionNotSatisfied()
    {
        var rule = CreateActiveRule("GENERAL", priority: 100);
        rule.Deactivate();
        rule.AddExceptionCondition("OfficeCode", AccountingConditionOperator.Equals, "SPECIAL-OFFICE");
        rule.Activate();

        var context = new Dictionary<string, string> { ["OfficeCode"] = "NORMAL-OFFICE" };
        var result = AccountingRuleResolver.Resolve(new[] { rule }, Module, context, AsOfDate);

        result.Should().Be(rule);
    }

    [Fact]
    public void Resolve_GeneralPlusConditionalOverride_PicksOverrideWhenItsConditionMatches()
    {
        var general = CreateActiveRule("GENERAL", priority: 100); // no conditions - always matches
        var overrideRule = CreateActiveRule("LARGE-AMOUNT", priority: 10);
        overrideRule.Deactivate();
        overrideRule.AddMatchCondition("Amount", AccountingConditionOperator.GreaterThan, "50000");
        overrideRule.Activate();

        var context = new Dictionary<string, string> { ["Amount"] = "100000" };
        var result = AccountingRuleResolver.Resolve(new[] { general, overrideRule }, Module, context, AsOfDate);

        result.Should().Be(overrideRule);
    }

    [Fact]
    public void Resolve_GeneralPlusConditionalOverride_FallsBackToGeneralWhenOverrideConditionDoesNotMatch()
    {
        var general = CreateActiveRule("GENERAL", priority: 100);
        var overrideRule = CreateActiveRule("LARGE-AMOUNT", priority: 10);
        overrideRule.Deactivate();
        overrideRule.AddMatchCondition("Amount", AccountingConditionOperator.GreaterThan, "50000");
        overrideRule.Activate();

        var context = new Dictionary<string, string> { ["Amount"] = "5000" };
        var result = AccountingRuleResolver.Resolve(new[] { general, overrideRule }, Module, context, AsOfDate);

        result.Should().Be(general);
    }

    [Fact]
    public void Resolve_BetweenOperatorCondition_MatchesWithinInclusiveBounds()
    {
        var rule = CreateActiveRule("RANGE", priority: 100);
        rule.Deactivate();
        rule.AddMatchCondition("Amount", AccountingConditionOperator.Between, "1000", "5000");
        rule.Activate();

        var withinRange = new Dictionary<string, string> { ["Amount"] = "5000" }; // upper bound, inclusive
        AccountingRuleResolver.Resolve(new[] { rule }, Module, withinRange, AsOfDate).Should().Be(rule);

        var outsideRange = new Dictionary<string, string> { ["Amount"] = "5000.01" };
        AccountingRuleResolver.Resolve(new[] { rule }, Module, outsideRange, AsOfDate).Should().BeNull();
    }

    [Fact]
    public void Resolve_RuleForDifferentSourceModuleCode_IsExcluded()
    {
        var otherModuleRule = CreateActiveRule("OTHER", priority: 100, sourceModuleCode: "EXPENSE_PAYMENT");

        var result = AccountingRuleResolver.Resolve(new[] { otherModuleRule }, Module, new Dictionary<string, string>(), AsOfDate);

        result.Should().BeNull();
    }

    [Fact]
    public void Resolve_TwoActiveRulesWithSamePriority_ThrowsAmbiguityException()
    {
        var ruleA = CreateActiveRule("A", priority: 100);
        var ruleB = CreateActiveRule("B", priority: 100);

        Action act = () => AccountingRuleResolver.Resolve(new[] { ruleA, ruleB }, Module, new Dictionary<string, string>(), AsOfDate);

        act.Should().Throw<DomainException>()
            .WithMessage("*Ambiguous*");
    }

    [Fact]
    public void Resolve_MissingContextFieldForCondition_TreatsConditionAsNotSatisfied()
    {
        var rule = CreateActiveRule("COND", priority: 100);
        rule.Deactivate();
        rule.AddMatchCondition("OfficeCode", AccountingConditionOperator.Equals, "BANK-01");
        rule.Activate();

        // Context does not contain "OfficeCode" at all.
        var result = AccountingRuleResolver.Resolve(new[] { rule }, Module, new Dictionary<string, string>(), AsOfDate);

        result.Should().BeNull();
    }
}
