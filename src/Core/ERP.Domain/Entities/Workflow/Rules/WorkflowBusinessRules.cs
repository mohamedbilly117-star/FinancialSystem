using ERP.Domain.Interfaces;

namespace ERP.Domain.Entities.Workflow.Rules;

/// <summary>Prompt 10 - Workflow Engine: a template with zero configured levels could never actually route anything for approval.</summary>
public sealed class WorkflowTemplateMustHaveAtLeastOneLevelRule : IBusinessRule
{
    private readonly int _levelCount;

    public WorkflowTemplateMustHaveAtLeastOneLevelRule(int levelCount) => _levelCount = levelCount;

    public bool IsSatisfied() => _levelCount >= 1;

    public string Message => $"A workflow template must have at least one approval level defined; this template has {_levelCount}.";
}
