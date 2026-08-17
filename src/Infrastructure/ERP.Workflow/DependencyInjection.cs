using Microsoft.Extensions.DependencyInjection;

namespace ERP.Workflow;

/// <summary>Marker used by ERP.ArchitectureTests (<c>typeof(IWorkflowMarker).Assembly</c>).</summary>
public interface IWorkflowMarker
{
}

/// <summary>
/// Composition entry point for the Workflow layer. The Workflow Engine's
/// Domain entities (<c>WorkflowTemplate</c>, <c>ApprovalLevelDefinition</c>,
/// <c>WorkflowInstance</c>, <c>ApprovalAction</c> - Prompt 10) are now
/// built in ERP.Domain and consumed directly via
/// <c>IApplicationDbContext</c>, exactly like every other module in this
/// solution (Accounting, Distribution, Rule Engine, Security) - none of
/// them needed a dedicated cross-cutting service registered here, and
/// neither does this one yet. This stays an explicit, empty extension
/// point for whatever genuinely cross-cutting Workflow concern turns out
/// to need one once Application-layer CQRS handlers are built (a later
/// roadmap phase) - e.g. a background escalation-checking job.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddWorkflow(this IServiceCollection services)
    {
        // Intentionally empty - see class remarks above.
        return services;
    }
}
