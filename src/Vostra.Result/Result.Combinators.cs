namespace Vostra.Result;

public readonly partial struct Result
{
    /// <summary>Chains another valueless operation on success; propagates errors otherwise.</summary>
    public Result Then(Func<Result> next) => IsError ? this : next();

    /// <summary>Chains a value-producing operation on success; propagates errors otherwise.</summary>
    public Result<T> Then<T>(Func<Result<T>> next) => IsError ? Result<T>.Err(ErrorArray) : next();

    /// <summary>Runs <paramref name="action"/> on success and returns this result unchanged.</summary>
    public Result Tap(Action action)
    {
        if (IsSuccess) { action(); }
        return this;
    }

    /// <summary>Runs <paramref name="action"/> on failure and returns this result unchanged.</summary>
    public Result TapError(Action<IReadOnlyList<ErrorBase>> action)
    {
        if (IsError) { action(Errors); }
        return this;
    }

    /// <summary>Transforms each error on failure; passes successes through.</summary>
    public Result MapError(Func<ErrorBase, ErrorBase> map) =>
        IsSuccess ? this : FromErrors(Array.ConvertAll(ErrorArray, e => map(e)));

    /// <summary>Fails with <paramref name="error"/> when <paramref name="predicate"/> is false on a success.</summary>
    public Result Ensure(Func<bool> predicate, ErrorBase error) =>
        IsError ? this : (predicate() ? this : Failure(error));

    /// <summary>Fails with a <see cref="ValidationError"/> when <paramref name="predicate"/> is false.</summary>
    public Result Ensure(Func<bool> predicate, string validationMessage) =>
        Ensure(predicate, new ValidationError(validationMessage));

    /// <summary>Async <see cref="Then(Func{Result})"/>.</summary>
    public Task<Result> Then(Func<Task<Result>> next) =>
        IsError ? Task.FromResult(this) : next();

    /// <summary>Async <see cref="Then{T}(Func{Result{T}})"/>.</summary>
    public Task<Result<T>> Then<T>(Func<Task<Result<T>>> next) =>
        IsError ? Task.FromResult(Result<T>.Err(ErrorArray)) : next();
}
