namespace Vostra.Results;

/// <summary>Input failed validation.</summary>
public sealed class ValidationError : ErrorBase
{
    /// <summary>Creates a <see cref="ValidationError"/>.</summary>
    public ValidationError(
        string message,
        string code = "General.Validation",
        Exception? causedBy = null,
        IReadOnlyDictionary<string, object?>? metadata = null)
        : base(code, message, ErrorType.Validation, causedBy, metadata) { }

    /// <inheritdoc />
    protected override ErrorBase CloneWith(string code, string message, Exception? causedBy, IReadOnlyDictionary<string, object?>? metadata) =>
        new ValidationError(message, code, causedBy, metadata);
}

/// <summary>A requested resource was not found.</summary>
public sealed class NotFoundError : ErrorBase
{
    /// <summary>Creates a <see cref="NotFoundError"/>.</summary>
    public NotFoundError(
        string message,
        string code = "General.NotFound",
        Exception? causedBy = null,
        IReadOnlyDictionary<string, object?>? metadata = null)
        : base(code, message, ErrorType.NotFound, causedBy, metadata) { }

    /// <inheritdoc />
    protected override ErrorBase CloneWith(string code, string message, Exception? causedBy, IReadOnlyDictionary<string, object?>? metadata) =>
        new NotFoundError(message, code, causedBy, metadata);
}

/// <summary>The request conflicts with current state.</summary>
public sealed class ConflictError : ErrorBase
{
    /// <summary>Creates a <see cref="ConflictError"/>.</summary>
    public ConflictError(
        string message,
        string code = "General.Conflict",
        Exception? causedBy = null,
        IReadOnlyDictionary<string, object?>? metadata = null)
        : base(code, message, ErrorType.Conflict, causedBy, metadata) { }

    /// <inheritdoc />
    protected override ErrorBase CloneWith(string code, string message, Exception? causedBy, IReadOnlyDictionary<string, object?>? metadata) =>
        new ConflictError(message, code, causedBy, metadata);
}

/// <summary>A resource already exists (a conflict).</summary>
public sealed class AlreadyExistsError : ErrorBase
{
    /// <summary>Creates an <see cref="AlreadyExistsError"/>.</summary>
    public AlreadyExistsError(
        string message,
        string code = "General.AlreadyExists",
        Exception? causedBy = null,
        IReadOnlyDictionary<string, object?>? metadata = null)
        : base(code, message, ErrorType.Conflict, causedBy, metadata) { }

    /// <inheritdoc />
    protected override ErrorBase CloneWith(string code, string message, Exception? causedBy, IReadOnlyDictionary<string, object?>? metadata) =>
        new AlreadyExistsError(message, code, causedBy, metadata);
}

/// <summary>Authentication is required or failed.</summary>
public sealed class UnauthorizedError : ErrorBase
{
    /// <summary>Creates an <see cref="UnauthorizedError"/>.</summary>
    public UnauthorizedError(
        string message,
        string code = "General.Unauthorized",
        Exception? causedBy = null,
        IReadOnlyDictionary<string, object?>? metadata = null)
        : base(code, message, ErrorType.Unauthorized, causedBy, metadata) { }

    /// <inheritdoc />
    protected override ErrorBase CloneWith(string code, string message, Exception? causedBy, IReadOnlyDictionary<string, object?>? metadata) =>
        new UnauthorizedError(message, code, causedBy, metadata);
}

/// <summary>The caller is authenticated but not permitted.</summary>
public sealed class ForbiddenError : ErrorBase
{
    /// <summary>Creates a <see cref="ForbiddenError"/>.</summary>
    public ForbiddenError(
        string message,
        string code = "General.Forbidden",
        Exception? causedBy = null,
        IReadOnlyDictionary<string, object?>? metadata = null)
        : base(code, message, ErrorType.Forbidden, causedBy, metadata) { }

    /// <inheritdoc />
    protected override ErrorBase CloneWith(string code, string message, Exception? causedBy, IReadOnlyDictionary<string, object?>? metadata) =>
        new ForbiddenError(message, code, causedBy, metadata);
}

/// <summary>
/// A general, unexpected fault (the 500-class error). Intended for boundary
/// exception-translation (catch a throw at an HTTP/test/messaging edge and carry it
/// via <see cref="ErrorBase.CausedBy"/>) and truly-unexpected faults — NOT a routine
/// return from domain logic. Expected failures should use the typed kinds
/// (<see cref="ValidationError"/>, <see cref="NotFoundError"/>, etc.) and genuine bugs should throw.
/// </summary>
public sealed class Error : ErrorBase
{
    /// <summary>Creates an <see cref="Error"/>.</summary>
    public Error(
        string message,
        string code = "General.Unexpected",
        Exception? causedBy = null,
        IReadOnlyDictionary<string, object?>? metadata = null)
        : base(code, message, ErrorType.Unexpected, causedBy, metadata) { }

    /// <inheritdoc />
    protected override ErrorBase CloneWith(string code, string message, Exception? causedBy, IReadOnlyDictionary<string, object?>? metadata) =>
        new Error(message, code, causedBy, metadata);
}
