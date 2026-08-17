using ERP.Domain.Common;
using ERP.Domain.Enums;
using ERP.Domain.Exceptions;
using ERP.Shared.Guards;

namespace ERP.Domain.Entities.Workflow;

/// <summary>
/// Prompt 10 - "Workflow Instances." One running (or completed) approval
/// chain for one specific business entity - e.g. "JournalEntry #4821 is
/// currently awaiting Level 2 approval." <see cref="SourceEntityType"/> +
/// <see cref="SourceEntityId"/> is the same forward-reference pattern used
/// throughout this solution (e.g. <c>JournalEntry.SourceReferenceId</c>) -
/// this Domain layer has no compile-time knowledge of which concrete
/// entity type it is tracking.
/// </summary>
public sealed class WorkflowInstance : AuditableEntity, IAggregateRoot
{
    private readonly List<ApprovalAction> _actions = new();

    public Guid WorkflowTemplateId { get; private set; }

    public string SourceEntityType { get; private set; } = string.Empty;

    public Guid SourceEntityId { get; private set; }

    /// <summary>Snapshot of the template's level count at the moment this instance started - later changes to the template (a new version) must never retroactively alter an already-running instance's expected level count.</summary>
    public int TotalLevels { get; private set; }

    public int CurrentLevelNumber { get; private set; }

    public WorkflowInstanceStatus Status { get; private set; }

    public DateTime StartedAtUtc { get; private set; }

    public DateTime? CompletedAtUtc { get; private set; }

    public IReadOnlyCollection<ApprovalAction> Actions => _actions.AsReadOnly();

    private WorkflowInstance()
    {
        // Required by EF Core.
    }

    private WorkflowInstance(Guid id, Guid workflowTemplateId, string sourceEntityType, Guid sourceEntityId, int totalLevels, DateTime startedAtUtc)
    {
        Id = id;
        WorkflowTemplateId = workflowTemplateId;
        SourceEntityType = sourceEntityType;
        SourceEntityId = sourceEntityId;
        TotalLevels = totalLevels;
        CurrentLevelNumber = 1;
        Status = WorkflowInstanceStatus.InProgress;
        StartedAtUtc = startedAtUtc;
    }

    /// <summary>Starts tracking approval progress for one specific entity against an Active template. The template must be Active and have at least one level - both already guaranteed by <see cref="WorkflowTemplate.Activate"/>, re-checked here defensively since a template's mutable <c>IsActive</c> flag could theoretically change between when it was looked up and when this is called.</summary>
    public static WorkflowInstance Start(WorkflowTemplate template, Guid sourceEntityId, DateTime startedAtUtc)
    {
        Guard.AgainstNull(template, nameof(template));
        Guard.AgainstEmpty(sourceEntityId, nameof(sourceEntityId));

        if (!template.IsActive)
        {
            throw new DomainException($"Cannot start a workflow instance from an inactive template '{template.Code}'.");
        }

        if (template.Levels.Count == 0)
        {
            throw new DomainException($"Workflow template '{template.Code}' has no approval levels defined.");
        }

        return new WorkflowInstance(Guid.NewGuid(), template.Id, template.SourceModuleCode, sourceEntityId, template.Levels.Count, startedAtUtc);
    }

    /// <summary>Records approval at the current level. If this was the final level, the instance completes as Approved; otherwise it advances to the next level and remains InProgress.</summary>
    public void RecordApproval(Guid actorUserId, string? comments, DateTime actionAtUtc)
    {
        EnsureInProgress();

        var action = ApprovalAction.CreateApproval(Id, CurrentLevelNumber, actorUserId, comments, actionAtUtc);
        _actions.Add(action);

        if (CurrentLevelNumber >= TotalLevels)
        {
            Status = WorkflowInstanceStatus.Approved;
            CompletedAtUtc = actionAtUtc;
        }
        else
        {
            CurrentLevelNumber++;
        }
    }

    /// <summary>A rejection at ANY level immediately ends the whole chain as Rejected - there is no "reject just this level and continue" concept (Prompt 10's Business Process Lifecycle treats Rejected as a terminal state, matching <c>JournalEntry.Reject</c>'s own single-step behavior).</summary>
    public void RecordRejection(Guid actorUserId, string comments, DateTime actionAtUtc)
    {
        EnsureInProgress();

        var action = ApprovalAction.CreateRejection(Id, CurrentLevelNumber, actorUserId, comments, actionAtUtc);
        _actions.Add(action);

        Status = WorkflowInstanceStatus.Rejected;
        CompletedAtUtc = actionAtUtc;
    }

    public void Cancel(DateTime cancelledAtUtc)
    {
        EnsureInProgress();

        Status = WorkflowInstanceStatus.Cancelled;
        CompletedAtUtc = cancelledAtUtc;
    }

    private void EnsureInProgress()
    {
        if (Status != WorkflowInstanceStatus.InProgress)
        {
            throw new DomainException($"Workflow instance is not InProgress (current status: {Status}).");
        }
    }
}

/// <summary>One recorded approval or rejection at one level of one <see cref="WorkflowInstance"/>. Child entity - only ever created through the owning instance's <see cref="WorkflowInstance.RecordApproval"/>/<see cref="WorkflowInstance.RecordRejection"/>.</summary>
public sealed class ApprovalAction : BaseEntity
{
    public Guid WorkflowInstanceId { get; private set; }

    public int LevelNumber { get; private set; }

    public Guid ActorUserId { get; private set; }

    public ApprovalActionType ActionType { get; private set; }

    public string? Comments { get; private set; }

    public DateTime ActionAtUtc { get; private set; }

    private ApprovalAction()
    {
        // Required by EF Core.
    }

    private ApprovalAction(Guid id, Guid workflowInstanceId, int levelNumber, Guid actorUserId, ApprovalActionType actionType, string? comments, DateTime actionAtUtc)
    {
        Id = id;
        WorkflowInstanceId = workflowInstanceId;
        LevelNumber = levelNumber;
        ActorUserId = actorUserId;
        ActionType = actionType;
        Comments = comments;
        ActionAtUtc = actionAtUtc;
    }

    internal static ApprovalAction CreateApproval(Guid workflowInstanceId, int levelNumber, Guid actorUserId, string? comments, DateTime actionAtUtc)
    {
        Guard.AgainstEmpty(actorUserId, nameof(actorUserId));

        return new ApprovalAction(Guid.NewGuid(), workflowInstanceId, levelNumber, actorUserId, ApprovalActionType.Approved, comments, actionAtUtc);
    }

    /// <summary>Unlike an approval, a rejection always requires a stated reason (Prompt 6 - Audit Details: "Reason") - there is no legitimate "reject with no explanation" case in a government financial system.</summary>
    internal static ApprovalAction CreateRejection(Guid workflowInstanceId, int levelNumber, Guid actorUserId, string comments, DateTime actionAtUtc)
    {
        Guard.AgainstEmpty(actorUserId, nameof(actorUserId));
        Guard.AgainstNullOrWhiteSpace(comments, nameof(comments));

        return new ApprovalAction(Guid.NewGuid(), workflowInstanceId, levelNumber, actorUserId, ApprovalActionType.Rejected, comments, actionAtUtc);
    }
}
