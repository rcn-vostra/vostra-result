namespace Vostra.Results;

/// <summary>
/// Async-receiver <b>terminal</b> overloads — let a <c>Task&lt;Result&lt;…&gt;&gt;</c> chain end fluently
/// without a wrapping <c>await</c>. Each awaits the source then delegates to the matching synchronous
/// terminal. Match/Switch arms stay synchronous; an arm that does async work returns a <c>Task</c>, which
/// you await by awaiting the whole expression.
/// </summary>
public static partial class ResultAsyncExtensions
{
    // ---- Result<T> ----

    /// <summary>Async-receiver <see cref="Result{T}.Match{TOut}"/>.</summary>
    public static async Task<TOut> Match<T, TOut>(
        this Task<Result<T>> source, Func<T, TOut> onOk, Func<IReadOnlyList<ErrorBase>, TOut> onErr) =>
        (await source.ConfigureAwait(false)).Match(onOk, onErr);

    /// <summary>Async-receiver <see cref="Result{T}.MatchFirst{TOut}"/>.</summary>
    public static async Task<TOut> MatchFirst<T, TOut>(
        this Task<Result<T>> source, Func<T, TOut> onOk, Func<ErrorBase, TOut> onFirstError) =>
        (await source.ConfigureAwait(false)).MatchFirst(onOk, onFirstError);

    /// <summary>Async-receiver <see cref="Result{T}.Switch"/>.</summary>
    public static async Task Switch<T>(
        this Task<Result<T>> source, Action<T> onOk, Action<IReadOnlyList<ErrorBase>> onErr)
    {
        var result = await source.ConfigureAwait(false);
        result.Switch(onOk, onErr);
    }

    /// <summary>Async-receiver <see cref="Result{T}.SwitchFirst"/>.</summary>
    public static async Task SwitchFirst<T>(
        this Task<Result<T>> source, Action<T> onOk, Action<ErrorBase> onFirstError)
    {
        var result = await source.ConfigureAwait(false);
        result.SwitchFirst(onOk, onFirstError);
    }

    /// <summary>Async-receiver <see cref="Result{T}.GetValueOr(T)"/>.</summary>
    public static async Task<T> GetValueOr<T>(this Task<Result<T>> source, T fallback) =>
        (await source.ConfigureAwait(false)).GetValueOr(fallback);

    /// <summary>Async-receiver <see cref="Result{T}.GetValueOr(Func{IReadOnlyList{ErrorBase}, T})"/>.</summary>
    public static async Task<T> GetValueOr<T>(this Task<Result<T>> source, Func<IReadOnlyList<ErrorBase>, T> fallback) =>
        (await source.ConfigureAwait(false)).GetValueOr(fallback);

    // ---- Result<T1, T2> ----

    /// <summary>Async-receiver <see cref="Result{T1,T2}.Match{TOut}"/>.</summary>
    public static async Task<TOut> Match<T1, T2, TOut>(
        this Task<Result<T1, T2>> source,
        Func<T1, TOut> onT1, Func<T2, TOut> onT2, Func<IReadOnlyList<ErrorBase>, TOut> onErr) =>
        (await source.ConfigureAwait(false)).Match(onT1, onT2, onErr);

    /// <summary>Async-receiver <see cref="Result{T1,T2}.Switch"/>.</summary>
    public static async Task Switch<T1, T2>(
        this Task<Result<T1, T2>> source,
        Action<T1> onT1, Action<T2> onT2, Action<IReadOnlyList<ErrorBase>> onErr)
    {
        var result = await source.ConfigureAwait(false);
        result.Switch(onT1, onT2, onErr);
    }

    // ---- Result<T1, T2, T3> ----

    /// <summary>Async-receiver <see cref="Result{T1,T2,T3}.Match{TOut}"/>.</summary>
    public static async Task<TOut> Match<T1, T2, T3, TOut>(
        this Task<Result<T1, T2, T3>> source,
        Func<T1, TOut> onT1, Func<T2, TOut> onT2, Func<T3, TOut> onT3, Func<IReadOnlyList<ErrorBase>, TOut> onErr) =>
        (await source.ConfigureAwait(false)).Match(onT1, onT2, onT3, onErr);

    /// <summary>Async-receiver <see cref="Result{T1,T2,T3}.Switch"/>.</summary>
    public static async Task Switch<T1, T2, T3>(
        this Task<Result<T1, T2, T3>> source,
        Action<T1> onT1, Action<T2> onT2, Action<T3> onT3, Action<IReadOnlyList<ErrorBase>> onErr)
    {
        var result = await source.ConfigureAwait(false);
        result.Switch(onT1, onT2, onT3, onErr);
    }
}
