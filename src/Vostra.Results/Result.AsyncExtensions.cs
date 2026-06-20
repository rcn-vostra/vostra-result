namespace Vostra.Results;

/// <summary>Async-receiver overloads for the non-generic <see cref="Result"/> (mirrors the Result&lt;T&gt; matrix).</summary>
public static partial class ResultAsyncExtensions
{
    /// <summary>Async-receiver, sync valueless step.</summary>
    public static async Task<Result> Then(this Task<Result> source, Func<Result> next) =>
        (await source.ConfigureAwait(false)).Then(next);

    /// <summary>Async-receiver, async valueless step.</summary>
    public static async Task<Result> Then(this Task<Result> source, Func<Task<Result>> next) =>
        await (await source.ConfigureAwait(false)).Then(next).ConfigureAwait(false);

    /// <summary>Async-receiver, sync value step.</summary>
    public static async Task<Result<T>> Then<T>(this Task<Result> source, Func<Result<T>> next) =>
        (await source.ConfigureAwait(false)).Then(next);

    /// <summary>Async-receiver, async value step.</summary>
    public static async Task<Result<T>> Then<T>(this Task<Result> source, Func<Task<Result<T>>> next) =>
        await (await source.ConfigureAwait(false)).Then(next).ConfigureAwait(false);

    /// <summary>Async-receiver, sync side-effect.</summary>
    public static async Task<Result> Tap(this Task<Result> source, Action action) =>
        (await source.ConfigureAwait(false)).Tap(action);

    /// <summary>Async-receiver, async side-effect.</summary>
    public static async Task<Result> Tap(this Task<Result> source, Func<Task> action)
    {
        var result = await source.ConfigureAwait(false);
        if (result.IsSuccess) { await action().ConfigureAwait(false); }
        return result;
    }

    /// <summary>Async-receiver, sync error side-effect.</summary>
    public static async Task<Result> TapError(this Task<Result> source, Action<IReadOnlyList<ErrorBase>> action) =>
        (await source.ConfigureAwait(false)).TapError(action);

    /// <summary>Async-receiver, async error side-effect.</summary>
    public static async Task<Result> TapError(this Task<Result> source, Func<IReadOnlyList<ErrorBase>, Task> action)
    {
        var result = await source.ConfigureAwait(false);
        if (result.IsError) { await action(result.Errors).ConfigureAwait(false); }
        return result;
    }

    /// <summary>Async-receiver guard with explicit error.</summary>
    public static async Task<Result> Ensure(this Task<Result> source, Func<bool> predicate, ErrorBase error) =>
        (await source.ConfigureAwait(false)).Ensure(predicate, error);

    /// <summary>Async-receiver guard with validation message.</summary>
    public static async Task<Result> Ensure(this Task<Result> source, Func<bool> predicate, string validationMessage) =>
        (await source.ConfigureAwait(false)).Ensure(predicate, validationMessage);

    /// <summary>Async-receiver error transform.</summary>
    public static async Task<Result> MapError(this Task<Result> source, Func<ErrorBase, ErrorBase> map) =>
        (await source.ConfigureAwait(false)).MapError(map);
}
