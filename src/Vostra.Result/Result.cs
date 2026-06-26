namespace Vostra.Result;

/// <summary>
/// A success/failure result without a value, for void-returning operations.
/// Also the single discoverable entry point for explicit factories.
/// </summary>
public readonly partial struct Result : IEquatable<Result>
{
    private readonly ErrorBase[]? _errors;
    private readonly bool _initialized;
    private readonly SuccessKind _successKind;

    private Result(bool initialized, ErrorBase[]? errors, SuccessKind successKind)
    {
        _initialized = initialized;
        _errors = errors;
        _successKind = successKind;
    }

    /// <summary>A successful (valueless) result. The valueless sibling of <see cref="Ok{T}"/>, and the 200
    /// counterpart to <see cref="Created()"/> / <see cref="Created{T}"/> for the 201 case.</summary>
    public static Result Ok() => new(initialized: true, errors: null, SuccessKind.Ok);

    /// <summary>A successful (valueless) result that created a new resource.</summary>
    public static Result Created() => new(initialized: true, errors: null, SuccessKind.Created);

    /// <summary>Creates a successful <c>Result&lt;T&gt;</c> that created a new resource.</summary>
    public static Result<T> Created<T>(T value) => Result<T>.Created(value);

    internal static Result FromErrors(ErrorBase[] errors) =>
        // Empty/null error array would read as success; substitute the uninitialized sentinel so it stays faulted.
        new(initialized: true, errors: errors is { Length: > 0 } ? errors : ResultSentinels.UninitializedArray, SuccessKind.Ok);

    /// <summary>Creates a failed result from a single error.</summary>
    public static Result Failure(ErrorBase error) => FromErrors(new[] { error });

    /// <summary>Creates a successful <c>Result&lt;T&gt;</c> from a value.</summary>
    public static Result<T> Ok<T>(T value) => value;

    /// <summary>Creates a failed <c>Result&lt;T&gt;</c> from an error.</summary>
    public static Result<T> Fail<T>(ErrorBase error) => error;

    /// <summary>True when this result is a success.</summary>
    public bool IsSuccess => !IsError;

    /// <summary>The kind of success; <see cref="SuccessKind.Ok"/> for failures.</summary>
    public SuccessKind SuccessKind => _successKind;

    /// <summary>True when this result carries error(s), including the uninitialized <c>default</c>.</summary>
    public bool IsError => !_initialized || _errors is not null;

    /// <summary>The error(s); empty when this is a success.</summary>
    public IReadOnlyList<ErrorBase> Errors =>
        _errors ?? (_initialized ? Array.Empty<ErrorBase>() : ResultSentinels.UninitializedList);

    internal ErrorBase[] ErrorArray => _errors ?? ResultSentinels.UninitializedArray;

    /// <summary>The first error. Throws if this is a success.</summary>
    public ErrorBase FirstError =>
        IsError ? Errors[0] : throw new InvalidOperationException("Result is a success; there is no error.");

    /// <summary>
    /// Propagates this failure as a typed <c>Result&lt;T&gt;</c>, carrying the same errors — the clean way to
    /// return a valueless failure from a <c>Result&lt;T&gt;</c>-returning method. Throws if this is a success
    /// (a valueless success has no <typeparamref name="T"/> value to give).
    /// </summary>
    public Result<T> ToError<T>() =>
        IsError ? Result<T>.Err(ErrorArray)
                : throw new InvalidOperationException("Result is a success; cannot convert it to a failed Result<T>.");

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

        return IsError ? Errors.SequenceEqual(other.Errors) : _successKind == other._successKind;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Result other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() =>
        IsError ? Errors.Aggregate(17, (hash, error) => HashCode.Combine(hash, error)) : (int)_successKind;

    /// <summary>Value equality.</summary>
    public static bool operator ==(Result left, Result right) => left.Equals(right);

    /// <summary>Value inequality.</summary>
    public static bool operator !=(Result left, Result right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString() => IsError ? $"Error[{Errors.Count}]({FirstError})" : "Success";
}
