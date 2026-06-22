namespace Vostra.Results;

public readonly partial struct Result
{
    /// <summary>Combines results, accumulating every error; success only if all succeed.</summary>
    public static Result Combine(params Result[] results)
    {
        var errors = results.Where(r => r.IsError).SelectMany(r => r.Errors).ToArray();
        return errors.Length == 0 ? Success : FromErrors(errors);
    }

    /// <summary>Combines value-results into a single result carrying all values, or all errors.</summary>
    public static Result<IReadOnlyList<T>> Combine<T>(params Result<T>[] results)
    {
        var errors = results.Where(r => r.IsError).SelectMany(r => r.ErrorArray).ToArray();
        if (errors.Length > 0)
        {
            return Result<IReadOnlyList<T>>.Err(errors);
        }

        IReadOnlyList<T> values = Array.ConvertAll(results, r => r.UnsafeValue);
        return Result<IReadOnlyList<T>>.Ok(values);
    }
}

/// <summary>Throttled async projection that collects results, porting the reference repo's SelectAsync.</summary>
public static class ResultCollectionExtensions
{
    /// <summary>Combines a sequence of value-results into one, collecting values in order or accumulating all errors.</summary>
    public static Result<IReadOnlyList<T>> Combine<T>(this IEnumerable<Result<T>> results) =>
        Result.Combine(results as Result<T>[] ?? System.Linq.Enumerable.ToArray(results));

    /// <summary>Combines a sequence of valueless results into one, accumulating all errors.</summary>
    public static Result Combine(this IEnumerable<Result> results) =>
        Result.Combine(results as Result[] ?? System.Linq.Enumerable.ToArray(results));

    // NOTE: `selector` should return failures as Result values, not throw. A thrown exception
    // propagates out of Task.WhenAll and bypasses the errors-as-values contract.

    /// <summary>
    /// Projects each item through <paramref name="selector"/> with at most
    /// <paramref name="maxConcurrency"/> concurrent invocations, returning every per-item
    /// <see cref="Result{TOut}"/> in source order — successes and failures preserved verbatim
    /// (no combining). Use this when you need each outcome; use <see cref="SelectAsync{TIn,TOut}"/>
    /// when you want a single combined result.
    /// </summary>
    /// <remarks>
    /// Cancellation is observed at the throttle gate; in-flight selector calls run to completion
    /// unless the selector itself observes the token.
    /// </remarks>
    public static async Task<IReadOnlyList<Result<TOut>>> SelectResultsAsync<TIn, TOut>(
        this IEnumerable<TIn> source,
        Func<TIn, Task<Result<TOut>>> selector,
        int maxConcurrency,
        CancellationToken cancellationToken = default)
    {
        if (maxConcurrency < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxConcurrency), "Must be at least 1.");
        }

        using var gate = new SemaphoreSlim(maxConcurrency);

        var tasks = source.Select(async item =>
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await selector(item).ConfigureAwait(false);
            }
            finally
            {
                gate.Release();
            }
        }).ToArray();

        return await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    /// <summary>
    /// Projects each item through <paramref name="selector"/> with at most
    /// <paramref name="maxConcurrency"/> concurrent invocations, then combines the results
    /// (all values, or all errors accumulated). For the per-item outcomes instead of a combined
    /// result, use <see cref="SelectResultsAsync{TIn,TOut}"/>.
    /// </summary>
    public static async Task<Result<IReadOnlyList<TOut>>> SelectAsync<TIn, TOut>(
        this IEnumerable<TIn> source,
        Func<TIn, Task<Result<TOut>>> selector,
        int maxConcurrency) =>
        (await source.SelectResultsAsync(selector, maxConcurrency).ConfigureAwait(false)).Combine();
}
