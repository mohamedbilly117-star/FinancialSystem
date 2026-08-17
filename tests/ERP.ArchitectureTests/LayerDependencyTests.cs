using ERP.Application;
using ERP.Domain.Common;
using ERP.Infrastructure;
using ERP.Notifications;
using ERP.Persistence.Context;
using ERP.Reporting;
using ERP.Security;
using ERP.Shared;
using ERP.Workflow;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace ERP.ArchitectureTests;

/// <summary>
/// Enforces the layering rules approved in Prompt 3 (Enterprise Software
/// Architecture &amp; Solution Blueprint). If any of these tests fail, someone
/// has introduced a dependency that violates the approved architecture -
/// per Prompt 13's governance rules, that requires explicit user approval,
/// not a silent code change.
/// </summary>
public class LayerDependencyTests
{
    private const string DomainNamespace = "ERP.Domain";
    private const string ApplicationNamespace = "ERP.Application";
    private const string PersistenceNamespace = "ERP.Persistence";
    private const string InfrastructureNamespace = "ERP.Infrastructure";
    private const string SecurityNamespace = "ERP.Security";
    private const string WorkflowNamespace = "ERP.Workflow";
    private const string NotificationsNamespace = "ERP.Notifications";
    private const string ReportingNamespace = "ERP.Reporting";
    private const string SharedNamespace = "ERP.Shared";
    private const string WebNamespace = "ERP.Web";

    [Fact(DisplayName = "Domain must not depend on any other project")]
    public void Domain_Should_Not_HaveDependencyOnOtherProjects()
    {
        var otherProjects = new[]
        {
            ApplicationNamespace, PersistenceNamespace, InfrastructureNamespace,
            SecurityNamespace, WorkflowNamespace, NotificationsNamespace,
            ReportingNamespace, WebNamespace
        };

        var result = Types.InAssembly(typeof(BaseEntity).Assembly)
            .Should()
            .NotHaveDependencyOnAny(otherProjects)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            BuildFailureMessage(result, "ERP.Domain must remain framework- and layer-agnostic."));
    }

    [Fact(DisplayName = "Application must depend only on Domain and Shared")]
    public void Application_Should_Not_HaveDependencyOnOuterLayers()
    {
        var outerLayers = new[]
        {
            PersistenceNamespace, InfrastructureNamespace, SecurityNamespace,
            WorkflowNamespace, NotificationsNamespace, ReportingNamespace, WebNamespace
        };

        var result = Types.InAssembly(typeof(IApplicationMarker).Assembly)
            .Should()
            .NotHaveDependencyOnAny(outerLayers)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            BuildFailureMessage(result, "ERP.Application must depend only on Domain and Shared (Dependency Inversion)."));
    }

    [Fact(DisplayName = "Persistence must not be depended upon by Security (would create a cycle)")]
    public void Security_Should_Not_HaveDependencyOnPersistence()
    {
        var result = Types.InAssembly(typeof(ISecurityMarker).Assembly)
            .Should()
            .NotHaveDependencyOn(PersistenceNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            BuildFailureMessage(result, "ERP.Security must not reference ERP.Persistence - Persistence references Security, not the other way around."));
    }

    [Fact(DisplayName = "No inner/infrastructure project may depend on the Web composition root")]
    public void NoLayer_Should_HaveDependencyOnWeb()
    {
        var allNonWebAssemblies = new[]
        {
            typeof(BaseEntity).Assembly,
            typeof(IApplicationMarker).Assembly,
            typeof(ApplicationDbContext).Assembly,
            typeof(IInfrastructureMarker).Assembly,
            typeof(ISecurityMarker).Assembly,
            typeof(IWorkflowMarker).Assembly,
            typeof(INotificationsMarker).Assembly,
            typeof(IReportingMarker).Assembly,
            typeof(ISharedMarker).Assembly,
        };

        foreach (var assembly in allNonWebAssemblies)
        {
            var result = Types.InAssembly(assembly)
                .Should()
                .NotHaveDependencyOn(WebNamespace)
                .GetResult();

            result.IsSuccessful.Should().BeTrue(
                BuildFailureMessage(result, $"{assembly.GetName().Name} must not reference ERP.Web (the composition root is a leaf node)."));
        }
    }

    [Fact(DisplayName = "Shared kernel must not depend on Domain or any outer layer")]
    public void Shared_Should_Not_HaveDependencyOnAnything()
    {
        var everythingElse = new[]
        {
            DomainNamespace, ApplicationNamespace, PersistenceNamespace, InfrastructureNamespace,
            SecurityNamespace, WorkflowNamespace, NotificationsNamespace, ReportingNamespace, WebNamespace
        };

        var result = Types.InAssembly(typeof(ISharedMarker).Assembly)
            .Should()
            .NotHaveDependencyOnAny(everythingElse)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            BuildFailureMessage(result, "ERP.Shared must remain a dependency-free kernel usable from any layer."));
    }

    private static string BuildFailureMessage(TestResult result, string rule)
    {
        var offenders = result.FailingTypes is null
            ? "(no type details available)"
            : string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>());

        return $"{rule} Violating types: {offenders}";
    }
}
