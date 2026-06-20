namespace Vostra.Results;

public readonly partial struct Result<T>
{
    /// <summary>Async Map: maps the value with <paramref name="map"/> on success; propagates errors otherwise.</summary>
    public async Task<Result<U>> Map<U>(Func<T, Task<U>> map) =>
        IsError ? ToError<U>() : Result<U>.Ok(await map(UnsafeValue).ConfigureAwait(false));

    /// <summary>Async Then: binds to another result with <paramref name="next"/> on success; propagates errors otherwise.</summary>
    public Task<Result<U>> Then<U>(Func<T, Task<Result<U>>> next) =>
        IsError ? Task.FromResult(ToError<U>()) : next(UnsafeValue);

    /// <summary>Async Tap: runs <paramref name="action"/> on success and returns this result unchanged.</summary>
    public async Task<Result<T>> Tap(Func<T, Task> action)
    {
        if (IsSuccess)
        {
            await action(UnsafeValue).ConfigureAwait(false);
        }

        return this;
    }

    /// <summary>Async TapError: runs <paramref name="action"/> on failure and returns this result unchanged.</summary>
    public async Task<Result<T>> TapError(Func<IReadOnlyList<ErrorBase>, Task> action)
    {
        if (IsError)
        {
            await action(Errors).ConfigureAwait(false);
        }

        return this;
    }

    /// <summary>Async Ensure: fails with <paramref name="error"/> when <paramref name="predicate"/> is false on a success.</summary>
    public async Task<Result<T>> Ensure(Func<T, Task<bool>> predicate, ErrorBase error)
    {
        if (IsError)
        {
            return this;
        }

        return await predicate(UnsafeValue).ConfigureAwait(false) ? this : Result<T>.Err(new[] { error });
    }

    /// <summary>Async Ensure: fails with a ValidationError when <paramref name="predicate"/> is false.</summary>
    public Task<Result<T>> Ensure(Func<T, Task<bool>> predicate, string validationMessage) =>
        Ensure(predicate, new ValidationError(validationMessage));
}
