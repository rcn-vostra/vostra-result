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
    /// <summary>
    /// Projects each item through <paramref name="selector"/> with at most
    /// <paramref name="maxConcurrency"/> concurrent invocations, then combines the results.
    /// </summary>
    public static async Task<Result<IReadOnlyList<TOut>>> SelectAsync<TIn, TOut>(
        this IEnumerable<TIn> source,
        Func<TIn, Task<Result<TOut>>> selector,
        int maxConcurrency)
    {
        if (maxConcurrency < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxConcurrency), "Must be at least 1.");
        }

        using var gate = new SemaphoreSlim(maxConcurrency);

        var tasks = source.Select(async item =>
        {
            await gate.WaitAsync().ConfigureAwait(false);
            try
            {
                return await selector(item).ConfigureAwait(false);
            }
            finally
            {
                gate.Release();
            }
        }).ToArray();

        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        return Result.Combine(results);
    }
}
