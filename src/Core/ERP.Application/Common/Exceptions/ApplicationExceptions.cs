using FluentValidation.Results;

namespace ERP.Application.Common.Exceptions;

/// <summary>
/// Thrown by the FluentValidation pipeline when a request/command fails
/// input validation. Carries a dictionary of property -> error messages so
/// the Blazor Server UI (Prompt 8's "Validation Experience" - inline field
/// highlighting, per-field messages) can map each error back to the exact
/// form field.
/// </summary>
public class ValidationException : Exception
{
    public ValidationException()
        : base("One or more validation failures have occurred.")
    {
        Errors = new Dictionary<string, string[]>();
    }

    public ValidationException(IEnumerable<ValidationFailure> failures)
        : this()
    {
        Errors = failures
            .GroupBy(f => f.PropertyName, f => f.ErrorMessage)
            .ToDictionary(g => g.Key, g => g.ToArray());
    }

    public ValidationException(string message)
        : base(message)
    {
        Errors = new Dictionary<string, string[]>();
    }

    public IDictionary<string, string[]> Errors { get; }
}

/// <summary>
/// Thrown when a use case requests an entity by Id and it does not exist
/// (or is soft-deleted and therefore invisible per Prompt 4's soft-delete
/// rule). Distinct from an authorization failure - see
/// <see cref="ForbiddenAccessException"/>.
/// </summary>
public class NotFoundException : Exception
{
    public NotFoundException()
    {
    }

    public NotFoundException(string message)
        : base(message)
    {
    }

    public NotFoundException(string name, object key)
        : base($"Entity \"{name}\" ({key}) was not found.")
    {
    }
}

/// <summary>
/// Thrown by <c>IPermissionService.AuthorizeAsync</c> when the current user
/// lacks the required permission for the requested action (Prompt 6's
/// granular Permission Engine). ERP.Web maps this to a localized
/// "access denied" message and records an audit entry for the denied
/// attempt itself, per Prompt 6's "every action must be validated /
/// everything must be audited" principle.
/// </summary>
public class ForbiddenAccessException : Exception
{
    public ForbiddenAccessException()
        : base("You are not authorized to perform this action.")
    {
    }

    public ForbiddenAccessException(string message)
        : base(message)
    {
    }
}
