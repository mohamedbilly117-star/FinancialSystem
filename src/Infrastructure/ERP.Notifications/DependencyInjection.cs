using Microsoft.Extensions.DependencyInjection;

namespace ERP.Notifications;

/// <summary>Marker used by ERP.ArchitectureTests (<c>typeof(INotificationsMarker).Assembly</c>).</summary>
public interface INotificationsMarker
{
}

/// <summary>
/// Composition entry point for the Notification Engine (Prompt 10):
/// system, workflow, approval, reminder, warning, deadline and overdue
/// notifications now; Email/SMS/Push are explicitly future-scoped behind
/// the same abstraction. Implemented in a later milestone alongside the
/// Workflow Engine, since most notifications are workflow-triggered.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddNotifications(this IServiceCollection services)
    {
        // Intentionally empty in this scaffolding milestone.
        // Future registrations: INotificationSender (in-app now; Email/SMS/
        // Push future implementations behind the same interface), template
        // rendering, and per-user notification-permission filtering
        // (Prompt 6: "Users should receive only notifications related to
        // their responsibilities").
        return services;
    }
}
