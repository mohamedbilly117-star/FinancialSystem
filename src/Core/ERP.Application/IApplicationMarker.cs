namespace ERP.Application;

/// <summary>
/// Marker interface whose sole purpose is <c>typeof(IApplicationMarker).Assembly</c>
/// - used by:
///   1. ERP.ArchitectureTests, to assert this layer never depends on outer layers;
///   2. ERP.Application's own DependencyInjection.cs, to scan this assembly
///      for FluentValidation validators and AutoMapper profiles without
///      hardcoding a list that every future module would have to remember
///      to update.
/// </summary>
public interface IApplicationMarker
{
}
