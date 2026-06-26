namespace Vostra.Result;

/// <summary>
/// Base type for all errors. An error carries a stable <see cref="Code"/> identity,
/// a human-readable <see cref="Message"/>, a neutral <see cref="Type"/>, and optional
/// <see cref="CausedBy"/> / <see cref="Metadata"/>. Extend by subclassing.
/// </summary>
public abstract class ErrorBase : IEquatable<ErrorBase>
{
    /// <summary>
    /// Conventional <see cref="Metadata"/> key naming the input field an error refers to. When every error in a
    /// failed result is a <see cref="ValidationError"/>, the ASP.NET Core layer groups them by this key into the
    /// RFC 7807 <c>errors</c> field-map (falling back to <see cref="Code"/> when absent). Set it via the
    /// <c>field</c> parameter on <see cref="ValidationError"/> rather than writing metadata by hand.
    /// </summary>
    public const string FieldMetadataKey = "field";

    /// <summary>Initializes a new <see cref="ErrorBase"/>.</summary>
    protected ErrorBase(
        string code,
        string message,
        ErrorType type,
        Exception? causedBy = null,
        IReadOnlyDictionary<string, object?>? metadata = null)
    {
        Code = code;
        Message = message;
        Type = type;
        CausedBy = causedBy;
        Metadata = metadata;
    }

    /// <summary>Stable, machine-readable identity, e.g. "Order.NotFound".</summary>
    public string Code { get; }

    /// <summary>Human-readable description.</summary>
    public string Message { get; }

    /// <summary>Neutral failure taxonomy.</summary>
    public ErrorType Type { get; }

    /// <summary>Optional originating exception (opt-in).</summary>
    public Exception? CausedBy { get; }

    /// <summary>Optional metadata bag (opt-in).</summary>
    public IReadOnlyDictionary<string, object?>? Metadata { get; }

    /// <summary>Creates a copy of this error (same concrete type) with the given fields. Subclasses override.</summary>
    protected abstract ErrorBase CloneWith(
        string code,
        string message,
        Exception? causedBy,
        IReadOnlyDictionary<string, object?>? metadata);

    /// <summary>Returns a copy with a different <see cref="Code"/>, preserving the concrete type.</summary>
    public ErrorBase WithCode(string code) => CloneWith(code, Message, CausedBy, Metadata);

    /// <summary>Returns a copy whose <see cref="Message"/> is prefixed (e.g. "shipping: original").</summary>
    public ErrorBase Prefix(string prefix) => CloneWith(Code, $"{prefix}: {Message}", CausedBy, Metadata);

    /// <summary>Returns a copy with the given <see cref="Metadata"/>, preserving the concrete type.</summary>
    public ErrorBase WithMetadata(IReadOnlyDictionary<string, object?> metadata) => CloneWith(Code, Message, CausedBy, metadata);

    /// <summary>Returns a copy with the given <see cref="CausedBy"/>, preserving the concrete type.</summary>
    public ErrorBase WithCausedBy(Exception causedBy) => CloneWith(Code, Message, causedBy, Metadata);

    /// <summary>Value equality on concrete type, <see cref="Type"/>, <see cref="Code"/>, and <see cref="Message"/>.</summary>
    public bool Equals(ErrorBase? other) =>
        other is not null
        && GetType() == other.GetType()
        && Type == other.Type
        && Code == other.Code
        && Message == other.Message;

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as ErrorBase);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(GetType(), Type, Code, Message);

    /// <inheritdoc />
    public override string ToString() => $"{GetType().Name}({Code}: {Message})";
}
