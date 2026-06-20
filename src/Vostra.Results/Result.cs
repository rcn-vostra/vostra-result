namespace Vostra.Results;

/// <summary>
/// A success/failure result without a value, for void-returning operations.
/// Also the single discoverable entry point for explicit factories.
/// </summary>
public readonly partial struct Result : IEquatable<Result>
{
    private readonly ErrorBase[]? _errors;
    private readonly bool _initialized;

    private Result(bool initialized, ErrorBase[]? errors)
    {
        _initialized = initialized;
        _errors = errors;
    }

    /// <summary>A successful (valueless) result.</summary>
    public static Result Success { get; } = new(initialized: true, errors: null);

    internal static Result FromErrors(ErrorBase[] errors) =>
        // Empty/null error array would read as success; substitute the uninitialized sentinel so it stays faulted.
        new(initialized: true, errors: errors is { Length: > 0 } ? errors : ResultSentinels.UninitializedArray);

    /// <summary>Creates a failed result from a single error.</summary>
    public static Result Failure(ErrorBase error) => FromErrors(new[] { error });

    /// <summary>Creates a successful <c>Result&lt;T&gt;</c> from a value.</summary>
    public static Result<T> Ok<T>(T value) => value;

    /// <summary>Creates a failed <c>Result&lt;T&gt;</c> from an error.</summary>
    public static Result<T> Fail<T>(ErrorBase error) => error;

    /// <summary>True when this result is a success.</summary>
    public bool IsSuccess => !IsError;

    /// <summary>True when this result carries error(s), including the uninitialized <c>default</c>.</summary>
    public bool IsError => !_initialized || _errors is not null;

    /// <summary>The error(s); empty when this is a success.</summary>
    public IReadOnlyList<ErrorBase> Errors =>
        _errors ?? (_initialized ? Array.Empty<ErrorBase>() : ResultSentinels.UninitializedList);

    /// <summary>The first error. Throws if this is a success.</summary>
    public ErrorBase FirstError =>
        IsError ? Errors[0] : throw new InvalidOperationException("Result is a success; there is no error.");

    /// <summary>Creates a failed result from a single error.</summary>
    public static implicit operator Result(ErrorBase error) => Failure(error);

    /// <summary>Creates a failed result from an array of errors.</summary>
    public static implicit operator Result(ErrorBase[] errors) => FromErrors(errors);

    /// <inheritdoc />
    public bool Equals(Result other)
    {
        if (IsError != other.IsError)
        {
            return false;
        }

        return !IsError || Errors.SequenceEqual(other.Errors);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Result other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() =>
        IsError ? Errors.Aggregate(17, (hash, error) => HashCode.Combine(hash, error)) : 0;

    /// <summary>Value equality.</summary>
    public static bool operator ==(Result left, Result right) => left.Equals(right);

    /// <summary>Value inequality.</summary>
    public static bool operator !=(Result left, Result right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString() => IsError ? $"Error[{Errors.Count}]({FirstError})" : "Success";
}
