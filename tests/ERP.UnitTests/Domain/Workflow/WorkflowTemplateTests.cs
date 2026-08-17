using ERP.Domain.Entities.Workflow;
using ERP.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace ERP.UnitTests.Domain.Workflow;

public class WorkflowTemplateTests
{
    private static WorkflowTemplate CreateDraftTemplate()
        => WorkflowTemplate.CreateFirstVersion("JournalEntry", "JE-APPROVAL", "اعتماد قيد محاسبي", "Journal Entry Approval", new DateOnly(2026, 1, 1));

    [Fact]
    public void CreateFirstVersion_ValidInput_StartsAsInactiveVersionOneWithNoLevels()
    {
        var template = CreateDraftTemplate();

        template.IsActive.Should().BeFalse();
        template.Version.Should().Be(1);
        template.Levels.Should().BeEmpty();
        template.SourceModuleCode.Should().Be("JournalEntry");
    }

    [Fact]
    public void AddLevel_ValidInput_AssignsSequentialLevelNumbers()
    {
        var template = CreateDraftTemplate();

        var level1 = template.AddLevel("رئيس القسم", "Section Head", "JournalEntry.Approve");
        var level2 = template.AddLevel("المدير", "Director", "JournalEntry.Approve");

        level1.LevelNumber.Should().Be(1);
        level2.LevelNumber.Should().Be(2);
        template.Levels.Should().HaveCount(2);
    }

    [Fact]
    public void AddLevel_StoresRequiredPermissionCode_ForSecurityIntegration()
    {
        // The Application layer (a later milestone) checks this exact
        // string against IPermissionService.HasPermissionAsync - format
        // must match PermissionActions.For("JournalEntry", "Approve").
        var template = CreateDraftTemplate();

        var level = template.AddLevel("رئيس القسم", "Section Head", "JournalEntry.Approve");

        level.RequiredPermissionCode.Should().Be("JournalEntry.Approve");
    }

    [Fact]
    public void AddLevel_WithMinimumGreaterThanMaximum_Throws()
    {
        var template = CreateDraftTemplate();

        Action act = () => template.AddLevel("رئيس القسم", "Section Head", "JournalEntry.Approve", 50000m, 10000m);

        act.Should().Throw<DomainException>()
            .WithMessage("*minimum amount cannot exceed*");
    }

    [Fact]
    public void AddLevel_AfterActivation_Throws()
    {
        var template = CreateDraftTemplate();
        template.AddLevel("رئيس القسم", "Section Head", "JournalEntry.Approve");
        template.Activate();

        Action act = () => template.AddLevel("المدير", "Director", "JournalEntry.Approve");

        act.Should().Throw<DomainException>()
            .WithMessage("*Deactivated*");
    }

    [Fact]
    public void Activate_WithNoLevels_Throws()
    {
        var template = CreateDraftTemplate();

        Action act = template.Activate;

        act.Should().Throw<BusinessRuleValidationException>()
            .WithMessage("*at least one approval level*");
    }

    [Fact]
    public void Activate_WithAtLeastOneLevel_Succeeds()
    {
        var template = CreateDraftTemplate();
        template.AddLevel("رئيس القسم", "Section Head", "JournalEntry.Approve");

        template.Activate();

        template.IsActive.Should().BeTrue();
    }

    [Fact]
    public void RemoveLastLevel_RemovesOnlyTheMostRecentlyAddedLevel()
    {
        var template = CreateDraftTemplate();
        template.AddLevel("رئيس القسم", "Section Head", "JournalEntry.Approve");
        template.AddLevel("المدير", "Director", "JournalEntry.Approve");

        template.RemoveLastLevel();

        template.Levels.Should().ContainSingle().Which.NameEn.Should().Be("Section Head");
    }

    [Fact]
    public void RemoveLastLevel_WithNoLevels_Throws()
    {
        var template = CreateDraftTemplate();

        Action act = template.RemoveLastLevel;

        act.Should().Throw<DomainException>()
            .WithMessage("*no levels to remove*");
    }

    [Fact]
    public void CreateNewVersion_WhileNotActive_Throws()
    {
        var template = CreateDraftTemplate();

        Action act = () => template.CreateNewVersion("JE-APPROVAL", "اعتماد", "Approval", new DateOnly(2026, 6, 1));

        act.Should().Throw<DomainException>()
            .WithMessage("*Active*");
    }

    [Fact]
    public void CreateNewVersion_HappyPath_ProducesVersionTwoAndClosesOldEffectiveRange()
    {
        var v1 = CreateDraftTemplate();
        v1.AddLevel("رئيس القسم", "Section Head", "JournalEntry.Approve");
        v1.Activate();

        var v2 = v1.CreateNewVersion("JE-APPROVAL", "اعتماد معدل", "Revised Approval", new DateOnly(2026, 6, 1));

        v2.Version.Should().Be(2);
        v2.IsActive.Should().BeFalse();
        v2.SourceModuleCode.Should().Be(v1.SourceModuleCode);
        v1.EffectiveTo.Should().Be(new DateOnly(2026, 5, 31));
        v1.IsActive.Should().BeTrue();
    }
}
