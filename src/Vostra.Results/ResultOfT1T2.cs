namespace Vostra.Results;

/// <summary>
/// Carries <em>one of</em> two typed success values (<typeparamref name="T1"/> or
/// <typeparamref name="T2"/>) or one-or-more <see cref="ErrorBase"/>s. A discriminated-union
/// success surface: produce one by returning a value, consume it via <c>Match</c>/<c>Switch</c>.
/// There is no chaining through the union (no Map/Then/LINQ) — it is a terminal match surface.
/// The success path does not allocate, and value-type arms do not box.
/// </summary>
public readonly partial struct Result<T1, T2> : IEquatable<Result<T1, T2>>
{
    private readonly T1? _value1;
    private readonly T2? _value2;
    private readonly ErrorBase[]? _errors;
    private readonly bool _initialized;
    private readonly int _index;

    private Result(int index, T1? value1, T2? value2, ErrorBase[]? errors)
    {
        _initialized = true;
        _index = index;
        _value1 = value1;
        _value2 = value2;
        _errors = errors;
    }

    /// <summary>Creates a success holding the first arm (<typeparamref name="T1"/>).</summary>
    public static Result<T1, T2> First(T1 value) => new(index: 1, value, value2: default, errors: null);

    /// <summary>Creates a success holding the second arm (<typeparamref name="T2"/>).</summary>
    public static Result<T1, T2> Second(T2 value) => new(index: 2, value1: default, value, errors: null);

    internal static Result<T1, T2> Err(ErrorBase[] errors) =>
        // Empty/null error array would read as success; substitute the uninitialized sentinel so it stays faulted.
        new(index: 0, value1: default, value2: default,
            errors: errors is { Length: > 0 } ? errors : ResultSentinels.UninitializedArray);

    /// <summary>True when this result carries one of the success values.</summary>
    public bool IsSuccess => !IsError;

    /// <summary>True when this result carries error(s), including the uninitialized <c>default</c>.</summary>
    public bool IsError => !_initialized || _errors is not null;

    /// <summary>The active arm: <c>1</c> or <c>2</c> on success; <c>0</c> when faulted.</summary>
    public int Index => IsError ? 0 : _index;

    /// <summary>The error(s); empty when this is a success.</summary>
    public IReadOnlyList<ErrorBase> Errors =>
        _errors ?? (_initialized ? Array.Empty<ErrorBase>() : ResultSentinels.UninitializedList);

    internal ErrorBase[] ErrorArray => _errors ?? ResultSentinels.UninitializedArray;

    /// <summary>The first error. Throws if this is a success.</summary>
    public ErrorBase FirstError =>
        IsError ? Errors[0] : throw new InvalidOperationException("Result is a success; there is no error.");

    /// <summary>Creates a success from a first-arm value.</summary>
    public static implicit operator Result<T1, T2>(T1 value) => First(value);

    /// <summary>Creates a success from a second-arm value.</summary>
    public static implicit operator Result<T1, T2>(T2 value) => Second(value);

    /// <summary>Creates a failure from a single error.</summary>
    public static implicit operator Result<T1, T2>(ErrorBase error) => Err(new[] { error });

    /// <summary>Creates a failure from an array of errors.</summary>
    public static implicit operator Result<T1, T2>(ErrorBase[] errors) => Err(errors);

    /// <summary>Creates a failure from a list of errors.</summary>
    public static implicit operator Result<T1, T2>(List<ErrorBase> errors) => Err(errors.ToArray());

    /// <inheritdoc />
    public bool Equals(Result<T1, T2> other)
    {
        if (IsError != other.IsError)
        {
            return false;
        }

        if (IsError)
        {
            return Errors.SequenceEqual(other.Errors);
        }

        if (_index != other._index)
        {
            return false;
        }

        return _index == 1
            ? EqualityComparer<T1>.Default.Equals(_value1, other._value1)
            : EqualityComparer<T2>.Default.Equals(_value2, other._value2);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Result<T1, T2> other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        if (IsError)
        {
            return Errors.Aggregate(17, (hash, error) => HashCode.Combine(hash, error));
        }

        var valueHash = _index == 1
            ? (_value1 is null ? 0 : EqualityComparer<T1>.Default.GetHashCode(_value1))
            : (_value2 is null ? 0 : EqualityComparer<T2>.Default.GetHashCode(_value2));
        return HashCode.Combine(_index, valueHash);
    }

    /// <summary>Value equality.</summary>
    public static bool operator ==(Result<T1, T2> left, Result<T1, T2> right) => left.Equals(right);

    /// <summary>Value inequality.</summary>
    public static bool operator !=(Result<T1, T2> left, Result<T1, T2> right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString() =>
        IsError ? $"Error[{Errors.Count}]({FirstError})" : $"Success({(_index == 1 ? _value1 : (object?)_value2)})";
}
