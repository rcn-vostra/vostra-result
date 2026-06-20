namespace Vostra.Results;

/// <summary>Input failed validation.</summary>
public sealed class ValidationError : Error
{
    /// <summary>Creates a <see cref="ValidationError"/>.</summary>
    public ValidationError(
        string message,
        string code = "General.Validation",
        Exception? causedBy = null,
        IReadOnlyDictionary<string, object?>? metadata = null)
        : base(code, message, ErrorType.Validation, causedBy, metadata) { }
}

/// <summary>A requested resource was not found.</summary>
public sealed class NotFoundError : Error
{
    /// <summary>Creates a <see cref="NotFoundError"/>.</summary>
    public NotFoundError(
        string message,
        string code = "General.NotFound",
        Exception? causedBy = null,
        IReadOnlyDictionary<string, object?>? metadata = null)
        : base(code, message, ErrorType.NotFound, causedBy, metadata) { }
}

/// <summary>The request conflicts with current state.</summary>
public sealed class ConflictError : Error
{
    /// <summary>Creates a <see cref="ConflictError"/>.</summary>
    public ConflictError(
        string message,
        string code = "General.Conflict",
        Exception? causedBy = null,
        IReadOnlyDictionary<string, object?>? metadata = null)
        : base(code, message, ErrorType.Conflict, causedBy, metadata) { }
}

/// <summary>A resource already exists (a conflict).</summary>
public sealed class AlreadyExistsError : Error
{
    /// <summary>Creates an <see cref="AlreadyExistsError"/>.</summary>
    public AlreadyExistsError(
        string message,
        string code = "General.AlreadyExists",
        Exception? causedBy = null,
        IReadOnlyDictionary<string, object?>? metadata = null)
        : base(code, message, ErrorType.Conflict, causedBy, metadata) { }
}

/// <summary>Authentication is required or failed.</summary>
public sealed class UnauthorizedError : Error
{
    /// <summary>Creates an <see cref="UnauthorizedError"/>.</summary>
    public UnauthorizedError(
        string message,
        string code = "General.Unauthorized",
        Exception? causedBy = null,
        IReadOnlyDictionary<string, object?>? metadata = null)
        : base(code, message, ErrorType.Unauthorized, causedBy, metadata) { }
}

/// <summary>The caller is authenticated but not permitted.</summary>
public sealed class ForbiddenError : Error
{
    /// <summary>Creates a <see cref="ForbiddenError"/>.</summary>
    public ForbiddenError(
        string message,
        string code = "General.Forbidden",
        Exception? causedBy = null,
        IReadOnlyDictionary<string, object?>? metadata = null)
        : base(code, message, ErrorType.Forbidden, causedBy, metadata) { }
}

/// <summary>An unexpected fault. Often wraps an exception via <see cref="Error.CausedBy"/>.</summary>
public sealed class UnexpectedError : Error
{
    /// <summary>Creates an <see cref="UnexpectedError"/>.</summary>
    public UnexpectedError(
        string message,
        string code = "General.Unexpected",
        Exception? causedBy = null,
        IReadOnlyDictionary<string, object?>? metadata = null)
        : base(code, message, ErrorType.Unexpected, causedBy, metadata) { }
}
