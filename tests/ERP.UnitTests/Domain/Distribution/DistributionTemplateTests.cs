using ERP.Domain.Entities.Accounting;
using ERP.Domain.Entities.Accounting.Distribution;
using ERP.Domain.Enums;
using ERP.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace ERP.UnitTests.Domain.Distribution;

public class DistributionTemplateTests
{
    private static Account CreatePostingAccount(string code)
        => Account.CreateRoot(code, $"حساب {code}", $"Account {code}", AccountType.Revenue, AccountNormalBalance.Credit, AccountClassification.Posting);

    private static DistributionTemplate CreatePercentageTemplate(Guid? sourceReferenceId = null)
        => DistributionTemplate.CreateFirstVersion(
            DistributionSourceType.RevenueCategory,
            sourceReferenceId ?? Guid.NewGuid(),
            code: "PARKING-SPLIT",
            nameAr: "توزيع رسوم مواقف السيارات",
            nameEn: "Parking Fees Split",
            method: DistributionMethod.Percentage,
            effectiveFrom: new DateOnly(2026, 1, 1));

    [Fact]
    public void CreateFirstVersion_StartsAsInactiveVersionOne()
    {
        var template = CreatePercentageTemplate();

        template.IsActive.Should().BeFalse();
        template.Version.Should().Be(1);
        template.Lines.Should().BeEmpty();
    }

    [Fact]
    public void AddLine_ToPercentageTemplate_WithFixedAmountOnly_Throws()
    {
        var template = CreatePercentageTemplate();
        var account = CreatePostingAccount("4201");

        Action act = () => template.AddLine(account, percentage: null, fixedAmount: 100m);

        act.Should().Throw<BusinessRuleValidationException>()
            .WithMessage("*Percentage-method*");
    }

    [Fact]
    public void AddLine_ToFixedAmountTemplate_WithPercentageOnly_Throws()
    {
        var template = DistributionTemplate.CreateFirstVersion(
            DistributionSourceType.ExpenseCategory,
            Guid.NewGuid(),
            "MAINT-FIXED",
            "صيانة - مبلغ ثابت",
            "Maintenance - Fixed",
            DistributionMethod.FixedAmount,
            new DateOnly(2026, 1, 1));
        var account = CreatePostingAccount("5301");

        Action act = () => template.AddLine(account, percentage: 50m, fixedAmount: null);

        act.Should().Throw<BusinessRuleValidationException>()
            .WithMessage("*FixedAmount-method*");
    }

    [Fact]
    public void AddLine_ToParentAccount_Throws()
    {
        var template = CreatePercentageTemplate();
        var parentAccount = Account.CreateRoot("4000", "الإيرادات", "Revenue", AccountType.Revenue, AccountNormalBalance.Credit, AccountClassification.Parent);

        Action act = () => template.AddLine(parentAccount, percentage: 100m, fixedAmount: null);

        act.Should().Throw<BusinessRuleValidationException>()
            .WithMessage("*Parent*");
    }

    [Fact]
    public void Activate_WithNoLines_Throws()
    {
        var template = CreatePercentageTemplate();

        Action act = template.Activate;

        act.Should().Throw<BusinessRuleValidationException>()
            .WithMessage("*at least one*");
    }

    [Fact]
    public void Activate_PercentageTemplate_NotTotaling100_Throws()
    {
        var template = CreatePercentageTemplate();
        var accountA = CreatePostingAccount("4201");
        var accountB = CreatePostingAccount("4202");
        template.AddLine(accountA, percentage: 60m, fixedAmount: null);
        template.AddLine(accountB, percentage: 30m, fixedAmount: null); // 90%, not 100%

        Action act = template.Activate;

        act.Should().Throw<ArgumentException>()
            .WithMessage("*100%*");
    }

    [Fact]
    public void Activate_PercentageTemplate_TotalingExactly100_Succeeds()
    {
        var template = CreatePercentageTemplate();
        var accountA = CreatePostingAccount("4201");
        var accountB = CreatePostingAccount("4202");
        template.AddLine(accountA, percentage: 70m, fixedAmount: null);
        template.AddLine(accountB, percentage: 30m, fixedAmount: null);

        template.Activate();

        template.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Activate_FixedAmountTemplate_DoesNotRequire100PercentTotal()
    {
        // Prompt 11 addendum scopes the "must total 100%" rule explicitly
        // to percentage-based templates; a FixedAmount template's lines
        // are absolute currency amounts and must not be forced to sum to
        // any particular total.
        var template = DistributionTemplate.CreateFirstVersion(
            DistributionSourceType.ExpenseCategory,
            Guid.NewGuid(),
            "MAINT-FIXED",
            "صيانة - مبلغ ثابت",
            "Maintenance - Fixed",
            DistributionMethod.FixedAmount,
            new DateOnly(2026, 1, 1));
        var account = CreatePostingAccount("5301");
        template.AddLine(account, percentage: null, fixedAmount: 500m);

        Action act = template.Activate;

        act.Should().NotThrow();
        template.IsActive.Should().BeTrue();
    }

    [Fact]
    public void AddLine_AfterActivation_Throws()
    {
        var template = CreatePercentageTemplate();
        var account = CreatePostingAccount("4201");
        template.AddLine(account, percentage: 100m, fixedAmount: null);
        template.Activate();

        var anotherAccount = CreatePostingAccount("4202");
        Action act = () => template.AddLine(anotherAccount, percentage: 50m, fixedAmount: null);

        act.Should().Throw<DomainException>()
            .WithMessage("*Deactivated*");
    }

    [Fact]
    public void Deactivate_ThenAddLine_BecomesEditableAgain()
    {
        var template = CreatePercentageTemplate();
        var account = CreatePostingAccount("4201");
        template.AddLine(account, percentage: 100m, fixedAmount: null);
        template.Activate();

        template.Deactivate();
        var secondAccount = CreatePostingAccount("4202");
        Action act = () => template.RemoveLine(template.Lines.First().Id);

        act.Should().NotThrow();
        template.AddLine(secondAccount, percentage: 100m, fixedAmount: null);
        template.Lines.Should().HaveCount(1);
    }

    [Fact]
    public void CreateNewVersion_WhileNotYetActive_Throws()
    {
        var template = CreatePercentageTemplate();

        Action act = () => template.CreateNewVersion("PARKING-SPLIT", "توزيع", "Split", DistributionMethod.Percentage, new DateOnly(2026, 6, 1));

        act.Should().Throw<DomainException>()
            .WithMessage("*Active*");
    }

    [Fact]
    public void CreateNewVersion_HappyPath_ProducesDraftVersionTwoAndClosesOldEffectiveRange()
    {
        var sourceId = Guid.NewGuid();
        var v1 = CreatePercentageTemplate(sourceId);
        var account = CreatePostingAccount("4201");
        v1.AddLine(account, percentage: 100m, fixedAmount: null);
        v1.Activate();

        var v2 = v1.CreateNewVersion("PARKING-SPLIT", "توزيع معدل", "Revised Split", DistributionMethod.Percentage, new DateOnly(2026, 6, 1));

        v2.Version.Should().Be(2);
        v2.IsActive.Should().BeFalse();
        v2.SourceReferenceId.Should().Be(sourceId);
        v2.EffectiveFrom.Should().Be(new DateOnly(2026, 6, 1));

        v1.EffectiveTo.Should().Be(new DateOnly(2026, 5, 31));
        v1.IsActive.Should().BeTrue(); // still administratively active - only its date window closed.
    }

    [Fact]
    public void CreateNewVersion_WithEffectiveFromNotAfterCurrent_Throws()
    {
        var v1 = CreatePercentageTemplate();
        var account = CreatePostingAccount("4201");
        v1.AddLine(account, percentage: 100m, fixedAmount: null);
        v1.Activate();

        Action act = () => v1.CreateNewVersion("PARKING-SPLIT", "توزيع", "Split", DistributionMethod.Percentage, new DateOnly(2026, 1, 1));

        act.Should().Throw<DomainException>()
            .WithMessage("*must be after*");
    }
}
