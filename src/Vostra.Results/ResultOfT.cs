namespace Vostra.Results;

/// <summary>
/// Carries either a value of type <typeparamref name="T"/> or one-or-more <see cref="ErrorBase"/>s.
/// The success path does not allocate. There is no public Value — consume via
/// Match, Switch, or the TryGet members (added in later tasks).
/// </summary>
public readonly partial struct Result<T> : IEquatable<Result<T>>
{
    private readonly T? _value;
    private readonly ErrorBase[]? _errors;
    private readonly bool _initialized;

    private Result(bool initialized, T? value, ErrorBase[]? errors)
    {
        _initialized = initialized;
        _value = value;
        _errors = errors;
    }

    internal static Result<T> Ok(T value) => new(initialized: true, value, errors: null);

    internal static Result<T> Err(ErrorBase[] errors) =>
        // Empty/null error array would read as success; substitute the uninitialized sentinel so it stays faulted.
        new(initialized: true, value: default,
            errors: errors is { Length: > 0 } ? errors : ResultSentinels.UninitializedArray);

    /// <summary>True when this result carries a value.</summary>
    public bool IsSuccess => !IsError;

    /// <summary>True when this result carries error(s), including the uninitialized <c>default</c>.</summary>
    public bool IsError => !_initialized || _errors is not null;

    /// <summary>The error(s); empty when this is a success.</summary>
    public IReadOnlyList<ErrorBase> Errors =>
        _errors ?? (_initialized ? Array.Empty<ErrorBase>() : ResultSentinels.UninitializedList);

    /// <summary>The first error. Throws if this is a success.</summary>
    public ErrorBase FirstError =>
        IsError ? Errors[0] : throw new InvalidOperationException("Result is a success; there is no error.");

    internal T UnsafeValue => _value!;

    internal ErrorBase[] ErrorArray => _errors ?? ResultSentinels.UninitializedArray;

    /// <summary>Re-tags this failure as a <c>Result&lt;U&gt;</c>, preserving the errors.</summary>
    internal Result<U> ToError<U>() => Result<U>.Err(ErrorArray);

    /// <summary>Creates a success from a value.</summary>
    public static implicit operator Result<T>(T value) => Ok(value);

    /// <summary>Creates a failure from a single error.</summary>
    public static implicit operator Result<T>(ErrorBase error) => Err(new[] { error });

    /// <summary>Creates a failure from an array of errors.</summary>
    public static implicit operator Result<T>(ErrorBase[] errors) => Err(errors);

    /// <summary>Creates a failure from a list of errors.</summary>
    public static implicit operator Result<T>(List<ErrorBase> errors) => Err(errors.ToArray());

    /// <inheritdoc />
    public bool Equals(Result<T> other)
    {
        if (IsError != other.IsError)
        {
            return false;
        }

        return IsError
            ? Errors.SequenceEqual(other.Errors)
            : EqualityComparer<T>.Default.Equals(_value, other._value);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Result<T> other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() =>
        IsError
            ? Errors.Aggregate(17, (hash, error) => HashCode.Combine(hash, error))
            : _value is null ? 0 : EqualityComparer<T>.Default.GetHashCode(_value);

    /// <summary>Value equality.</summary>
    public static bool operator ==(Result<T> left, Result<T> right) => left.Equals(right);

    /// <summary>Value inequality.</summary>
    public static bool operator !=(Result<T> left, Result<T> right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString() =>
        IsError ? $"ErrorBase[{Errors.Count}]({FirstError})" : $"Success({_value})";
}
