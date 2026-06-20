namespace Vostra.Results;

public readonly partial struct Result<T>
{
    /// <summary>Maps the value with <paramref name="map"/> on success; propagates errors otherwise.</summary>
    public Result<U> Map<U>(Func<T, U> map) =>
        IsError ? ToError<U>() : Result<U>.Ok(map(UnsafeValue));

    /// <summary>Binds to another result with <paramref name="next"/> on success; propagates errors otherwise.</summary>
    public Result<U> Then<U>(Func<T, Result<U>> next) =>
        IsError ? ToError<U>() : next(UnsafeValue);

    /// <summary>Runs <paramref name="action"/> on success and returns this result unchanged.</summary>
    public Result<T> Tap(Action<T> action)
    {
        if (IsSuccess)
        {
            action(UnsafeValue);
        }

        return this;
    }

    /// <summary>Runs <paramref name="action"/> on failure and returns this result unchanged.</summary>
    public Result<T> TapError(Action<IReadOnlyList<Error>> action)
    {
        if (IsError)
        {
            action(Errors);
        }

        return this;
    }

    /// <summary>Fails with <paramref name="error"/> when <paramref name="predicate"/> is false on a success.</summary>
    public Result<T> Ensure(Func<T, bool> predicate, Error error) =>
        IsError ? this : (predicate(UnsafeValue) ? this : Result<T>.Err(new[] { error }));

    /// <summary>Fails with a <see cref="ValidationError"/> when <paramref name="predicate"/> is false (FR-5.4).</summary>
    public Result<T> Ensure(Func<T, bool> predicate, string validationMessage) =>
        Ensure(predicate, new ValidationError(validationMessage));

    /// <summary>Transforms each error with <paramref name="map"/>; passes successes through.</summary>
    public Result<T> MapError(Func<Error, Error> map) =>
        IsSuccess ? this : Result<T>.Err(Array.ConvertAll(ErrorArray, e => map(e)));
}
