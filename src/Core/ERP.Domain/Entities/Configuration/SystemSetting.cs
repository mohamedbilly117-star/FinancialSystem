using ERP.Domain.Common;
using ERP.Shared.Guards;

namespace ERP.Domain.Entities.Configuration;

/// <summary>
/// Prompt 11 - System Configuration / System Parameters: "Administrators
/// must configure... without code changes." A generic, runtime-editable
/// key-value setting, e.g. Key="Security.MaxLoginAttempts", Value="5".
///
/// Deliberately generic rather than one strongly-typed entity per
/// individual setting (dozens of disparate, evolving settings across
/// Organization Info, Security Policies, Localization, Default Values,
/// Backup Policies, etc. per Prompt 11) - this mirrors the same
/// appsettings.json comment's own distinction between deployment-time
/// configuration (stays in appsettings.json) and administrator-editable-
/// at-runtime configuration (belongs in the database, which is exactly
/// what this entity is for). <see cref="Value"/> is stored as a string;
/// callers parse it according to the type they expect for a given
/// <see cref="Key"/> - this entity does not attempt to enforce a schema
/// per key, since that would require hardcoding the very set of settings
/// Prompt 11 wants to remain configurable without code changes.
/// </summary>
public sealed class SystemSetting : AuditableEntity, IAggregateRoot
{
    public string Key { get; private set; } = string.Empty;

    public string Value { get; private set; } = string.Empty;

    /// <summary>Grouping for an admin settings screen - e.g. "Organization", "Security", "Localization".</summary>
    public string Category { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    private SystemSetting()
    {
        // Required by EF Core.
    }

    private SystemSetting(Guid id, string key, string value, string category, string? description)
    {
        Id = id;
        Key = key;
        Value = value;
        Category = category;
        Description = description;
    }

    public static SystemSetting Create(string key, string value, string category, string? description = null)
    {
        Guard.AgainstNullOrWhiteSpace(key, nameof(key));
        Guard.AgainstLengthGreaterThan(key, 100, nameof(key));
        Guard.AgainstNullOrWhiteSpace(category, nameof(category));

        // Value itself may legitimately be an empty string (e.g. a setting
        // deliberately cleared/blank) - it is not Guard-validated as
        // required, only non-null (enforced by the non-nullable parameter
        // type combined with the WarningsAsErrors=Nullable build setting).
        return new SystemSetting(Guid.NewGuid(), key, value, category, description);
    }

    public void UpdateValue(string newValue) => Value = newValue;
}
