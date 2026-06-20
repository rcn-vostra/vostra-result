namespace Vostra.Results;

/// <summary>
/// Base type for all errors. An error carries a stable <see cref="Code"/> identity,
/// a human-readable <see cref="Message"/>, a neutral <see cref="Type"/>, and optional
/// <see cref="CausedBy"/> / <see cref="Metadata"/>. Extend by subclassing.
/// </summary>
public abstract class ErrorBase : IEquatable<ErrorBase>
{
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
