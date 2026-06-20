namespace Vostra.Results;

/// <summary>
/// Async-receiver overloads. Each awaits the source result then delegates to the matching
/// instance combinator, so a chain "lifts" to Task&lt;Result&lt;&gt;&gt; on its first async step
/// and stays there — one leading await, sync and async steps interleave freely.
/// </summary>
public static class ResultAsyncExtensions
{
    // ---- Map ----
    /// <summary>Async-receiver, sync projection.</summary>
    public static async Task<Result<U>> Map<T, U>(this Task<Result<T>> source, Func<T, U> map) =>
        (await source.ConfigureAwait(false)).Map(map);

    /// <summary>Async-receiver, async projection.</summary>
    public static async Task<Result<U>> Map<T, U>(this Task<Result<T>> source, Func<T, Task<U>> map) =>
        await (await source.ConfigureAwait(false)).Map(map).ConfigureAwait(false);

    // ---- Then ----
    /// <summary>Async-receiver, sync projection.</summary>
    public static async Task<Result<U>> Then<T, U>(this Task<Result<T>> source, Func<T, Result<U>> next) =>
        (await source.ConfigureAwait(false)).Then(next);

    /// <summary>Async-receiver, async projection.</summary>
    public static async Task<Result<U>> Then<T, U>(this Task<Result<T>> source, Func<T, Task<Result<U>>> next) =>
        await (await source.ConfigureAwait(false)).Then(next).ConfigureAwait(false);

    // ---- Tap ----
    /// <summary>Async-receiver, sync side-effect.</summary>
    public static async Task<Result<T>> Tap<T>(this Task<Result<T>> source, Action<T> action) =>
        (await source.ConfigureAwait(false)).Tap(action);

    /// <summary>Async-receiver, async side-effect.</summary>
    public static async Task<Result<T>> Tap<T>(this Task<Result<T>> source, Func<T, Task> action) =>
        await (await source.ConfigureAwait(false)).Tap(action).ConfigureAwait(false);

    // ---- TapError ----
    /// <summary>Async-receiver, sync side-effect.</summary>
    public static async Task<Result<T>> TapError<T>(this Task<Result<T>> source, Action<IReadOnlyList<Error>> action) =>
        (await source.ConfigureAwait(false)).TapError(action);

    /// <summary>Async-receiver, async side-effect.</summary>
    public static async Task<Result<T>> TapError<T>(this Task<Result<T>> source, Func<IReadOnlyList<Error>, Task> action) =>
        await (await source.ConfigureAwait(false)).TapError(action).ConfigureAwait(false);

    // ---- Ensure ----
    /// <summary>Async-receiver, sync predicate, explicit error.</summary>
    public static async Task<Result<T>> Ensure<T>(this Task<Result<T>> source, Func<T, bool> predicate, Error error) =>
        (await source.ConfigureAwait(false)).Ensure(predicate, error);

    /// <summary>Async-receiver, sync predicate, validation message.</summary>
    public static async Task<Result<T>> Ensure<T>(this Task<Result<T>> source, Func<T, bool> predicate, string validationMessage) =>
        (await source.ConfigureAwait(false)).Ensure(predicate, validationMessage);

    /// <summary>Async-receiver, async predicate, explicit error.</summary>
    public static async Task<Result<T>> Ensure<T>(this Task<Result<T>> source, Func<T, Task<bool>> predicate, Error error) =>
        await (await source.ConfigureAwait(false)).Ensure(predicate, error).ConfigureAwait(false);

    /// <summary>Async-receiver, async predicate, validation message.</summary>
    public static async Task<Result<T>> Ensure<T>(this Task<Result<T>> source, Func<T, Task<bool>> predicate, string validationMessage) =>
        await (await source.ConfigureAwait(false)).Ensure(predicate, validationMessage).ConfigureAwait(false);

    // ---- MapError ----
    /// <summary>Async-receiver error transform.</summary>
    public static async Task<Result<T>> MapError<T>(this Task<Result<T>> source, Func<Error, Error> map) =>
        (await source.ConfigureAwait(false)).MapError(map);
}
