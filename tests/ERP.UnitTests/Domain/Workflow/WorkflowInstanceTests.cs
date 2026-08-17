using ERP.Domain.Entities.Workflow;
using ERP.Domain.Enums;
using ERP.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace ERP.UnitTests.Domain.Workflow;

public class WorkflowInstanceTests
{
    private static readonly DateTime Now = new(2026, 7, 15, 9, 0, 0, DateTimeKind.Utc);

    private static WorkflowTemplate CreateActiveTemplate(int levelCount)
    {
        var template = WorkflowTemplate.CreateFirstVersion("JournalEntry", "JE-APPROVAL", "اعتماد", "Approval", new DateOnly(2026, 1, 1));

        for (var i = 1; i <= levelCount; i++)
        {
            template.AddLevel($"مستوى {i}", $"Level {i}", "JournalEntry.Approve");
        }

        template.Activate();
        return template;
    }

    [Fact]
    public void Start_FromInactiveTemplate_Throws()
    {
        var template = WorkflowTemplate.CreateFirstVersion("JournalEntry", "JE-APPROVAL", "اعتماد", "Approval", new DateOnly(2026, 1, 1));
        template.AddLevel("مستوى 1", "Level 1", "JournalEntry.Approve");
        // Deliberately never Activate()d.

        Action act = () => WorkflowInstance.Start(template, Guid.NewGuid(), Now);

        act.Should().Throw<DomainException>()
            .WithMessage("*inactive*");
    }

    [Fact]
    public void Start_ValidActiveTemplate_CreatesInProgressInstanceAtLevelOne()
    {
        var template = CreateActiveTemplate(2);
        var journalEntryId = Guid.NewGuid();

        var instance = WorkflowInstance.Start(template, journalEntryId, Now);

        instance.Status.Should().Be(WorkflowInstanceStatus.InProgress);
        instance.CurrentLevelNumber.Should().Be(1);
        instance.TotalLevels.Should().Be(2);
        instance.SourceEntityType.Should().Be("JournalEntry");
        instance.SourceEntityId.Should().Be(journalEntryId);
        instance.StartedAtUtc.Should().Be(Now);
        instance.CompletedAtUtc.Should().BeNull();
    }

    [Fact]
    public void RecordApproval_SingleLevelTemplate_CompletesAsApprovedImmediately()
    {
        var template = CreateActiveTemplate(1);
        var instance = WorkflowInstance.Start(template, Guid.NewGuid(), Now);
        var approver = Guid.NewGuid();

        instance.RecordApproval(approver, "Looks correct", Now.AddHours(1));

        instance.Status.Should().Be(WorkflowInstanceStatus.Approved);
        instance.CompletedAtUtc.Should().Be(Now.AddHours(1));
        instance.Actions.Should().ContainSingle();
    }

    [Fact]
    public void RecordApproval_MultiLevelTemplate_AdvancesToNextLevelWithoutCompleting()
    {
        var template = CreateActiveTemplate(3);
        var instance = WorkflowInstance.Start(template, Guid.NewGuid(), Now);

        instance.RecordApproval(Guid.NewGuid(), null, Now.AddHours(1));

        instance.Status.Should().Be(WorkflowInstanceStatus.InProgress);
        instance.CurrentLevelNumber.Should().Be(2);
        instance.CompletedAtUtc.Should().BeNull();
    }

    [Fact]
    public void RecordApproval_MultiLevelTemplate_CompletesOnlyAfterFinalLevel()
    {
        var template = CreateActiveTemplate(3);
        var instance = WorkflowInstance.Start(template, Guid.NewGuid(), Now);

        instance.RecordApproval(Guid.NewGuid(), null, Now.AddHours(1));
        instance.Status.Should().Be(WorkflowInstanceStatus.InProgress);

        instance.RecordApproval(Guid.NewGuid(), null, Now.AddHours(2));
        instance.Status.Should().Be(WorkflowInstanceStatus.InProgress);

        instance.RecordApproval(Guid.NewGuid(), null, Now.AddHours(3));
        instance.Status.Should().Be(WorkflowInstanceStatus.Approved);

        instance.Actions.Should().HaveCount(3);
        instance.Actions.Select(a => a.LevelNumber).Should().BeEquivalentTo(new[] { 1, 2, 3 });
    }

    [Fact]
    public void RecordRejection_AtAnyLevel_ImmediatelyEndsAsRejected()
    {
        var template = CreateActiveTemplate(3);
        var instance = WorkflowInstance.Start(template, Guid.NewGuid(), Now);
        instance.RecordApproval(Guid.NewGuid(), null, Now.AddHours(1)); // advances to level 2

        instance.RecordRejection(Guid.NewGuid(), "Missing supporting documents", Now.AddHours(2));

        instance.Status.Should().Be(WorkflowInstanceStatus.Rejected);
        instance.CompletedAtUtc.Should().Be(Now.AddHours(2));
        instance.CurrentLevelNumber.Should().Be(2); // never advances further once rejected
    }

    [Fact]
    public void RecordRejection_WithoutComments_Throws()
    {
        var template = CreateActiveTemplate(1);
        var instance = WorkflowInstance.Start(template, Guid.NewGuid(), Now);

        Action act = () => instance.RecordRejection(Guid.NewGuid(), "", Now.AddHours(1));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RecordApproval_AfterInstanceAlreadyApproved_Throws()
    {
        var template = CreateActiveTemplate(1);
        var instance = WorkflowInstance.Start(template, Guid.NewGuid(), Now);
        instance.RecordApproval(Guid.NewGuid(), null, Now.AddHours(1));

        Action act = () => instance.RecordApproval(Guid.NewGuid(), null, Now.AddHours(2));

        act.Should().Throw<DomainException>()
            .WithMessage("*not InProgress*");
    }

    [Fact]
    public void RecordApproval_AfterInstanceAlreadyRejected_Throws()
    {
        var template = CreateActiveTemplate(2);
        var instance = WorkflowInstance.Start(template, Guid.NewGuid(), Now);
        instance.RecordRejection(Guid.NewGuid(), "Incorrect amount", Now.AddHours(1));

        Action act = () => instance.RecordApproval(Guid.NewGuid(), null, Now.AddHours(2));

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Cancel_WhileInProgress_Succeeds()
    {
        var template = CreateActiveTemplate(2);
        var instance = WorkflowInstance.Start(template, Guid.NewGuid(), Now);

        instance.Cancel(Now.AddHours(1));

        instance.Status.Should().Be(WorkflowInstanceStatus.Cancelled);
        instance.CompletedAtUtc.Should().Be(Now.AddHours(1));
    }

    [Fact]
    public void Cancel_AfterAlreadyCompleted_Throws()
    {
        var template = CreateActiveTemplate(1);
        var instance = WorkflowInstance.Start(template, Guid.NewGuid(), Now);
        instance.RecordApproval(Guid.NewGuid(), null, Now.AddHours(1));

        Action act = () => instance.Cancel(Now.AddHours(2));

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Actions_RecordCorrectActorAndLevelNumber()
    {
        var template = CreateActiveTemplate(2);
        var instance = WorkflowInstance.Start(template, Guid.NewGuid(), Now);
        var firstApprover = Guid.NewGuid();

        instance.RecordApproval(firstApprover, "OK", Now.AddHours(1));

        var recordedAction = instance.Actions.Single();
        recordedAction.ActorUserId.Should().Be(firstApprover);
        recordedAction.LevelNumber.Should().Be(1);
        recordedAction.ActionType.Should().Be(ApprovalActionType.Approved);
        recordedAction.Comments.Should().Be("OK");
    }
}
