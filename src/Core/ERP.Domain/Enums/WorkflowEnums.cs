namespace ERP.Domain.Enums;

/// <summary>Prompt 10 - Business Process Lifecycle applied to a workflow instance itself.</summary>
public enum WorkflowInstanceStatus
{
    InProgress = 1,
    Approved = 2,
    Rejected = 3,
    Cancelled = 4,
}

/// <summary>What one <c>ApprovalAction</c> recorded at a given level was.</summary>
public enum ApprovalActionType
{
    Approved = 1,
    Rejected = 2,
}
