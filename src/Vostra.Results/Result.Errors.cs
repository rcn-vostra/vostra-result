namespace Vostra.Results;

/// <summary>
/// Discoverable factories for the built-in error kinds. Type <c>Result.</c> and let IntelliSense
/// surface every failure — these mirror the concrete error classes one-for-one and are pure
/// pass-through sugar over their public constructors (so <c>new NotFoundError(...)</c> still works).
/// Each returns the concrete error type; the implicit <see cref="ErrorBase"/> → <see cref="Result"/> /
/// <c>Result&lt;T&gt;</c> conversions carry it to the call site.
/// </summary>
/// <remarks>
/// A <c>code</c> is worth supplying only when it carries information the kind and call-site context do not
/// already give — a validation field/rule (<c>"EmailAddress.Invalid"</c>), a specific conflict
/// (<c>"Order.AlreadyCancelled"</c>), or to tell apart same-kind failures for different entities. On a
/// single-entity getter, <c>"Order.NotFound"</c> merely restates the return type and the kind; skip it.
/// </remarks>
public readonly partial struct Result
{
    /// <summary>Creates a <see cref="Vostra.Results.ValidationError"/> (input failed validation).</summary>
    public static ValidationError ValidationError(
        string message,
        string code = "General.Validation",
        Exception? causedBy = null,
        IReadOnlyDictionary<string, object?>? metadata = null) =>
        new(message, code, causedBy, metadata);

    /// <summary>Creates a <see cref="Vostra.Results.NotFoundError"/> (a requested resource was not found).</summary>
    public static NotFoundError NotFoundError(
        string message,
        string code = "General.NotFound",
        Exception? causedBy = null,
        IReadOnlyDictionary<string, object?>? metadata = null) =>
        new(message, code, causedBy, metadata);

    /// <summary>Creates a <see cref="Vostra.Results.ConflictError"/> (the request conflicts with current state).</summary>
    public static ConflictError ConflictError(
        string message,
        string code = "General.Conflict",
        Exception? causedBy = null,
        IReadOnlyDictionary<string, object?>? metadata = null) =>
        new(message, code, causedBy, metadata);

    /// <summary>Creates an <see cref="Vostra.Results.AlreadyExistsError"/> (a resource already exists).</summary>
    public static AlreadyExistsError AlreadyExistsError(
        string message,
        string code = "General.AlreadyExists",
        Exception? causedBy = null,
        IReadOnlyDictionary<string, object?>? metadata = null) =>
        new(message, code, causedBy, metadata);

    /// <summary>Creates an <see cref="Vostra.Results.UnauthorizedError"/> (authentication is required or failed).</summary>
    public static UnauthorizedError UnauthorizedError(
        string message,
        string code = "General.Unauthorized",
        Exception? causedBy = null,
        IReadOnlyDictionary<string, object?>? metadata = null) =>
        new(message, code, causedBy, metadata);

    /// <summary>Creates a <see cref="Vostra.Results.ForbiddenError"/> (authenticated but not permitted).</summary>
    public static ForbiddenError ForbiddenError(
        string message,
        string code = "General.Forbidden",
        Exception? causedBy = null,
        IReadOnlyDictionary<string, object?>? metadata = null) =>
        new(message, code, causedBy, metadata);

    /// <summary>
    /// Creates an <see cref="Vostra.Results.Error"/> — the catch-all, unexpected (500-class) fault.
    /// Intended for boundary exception-translation and truly-unexpected faults, not routine domain failures.
    /// </summary>
    public static Error Error(
        string message,
        string code = "General.Unexpected",
        Exception? causedBy = null,
        IReadOnlyDictionary<string, object?>? metadata = null) =>
        new(message, code, causedBy, metadata);
}
