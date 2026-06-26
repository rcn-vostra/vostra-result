namespace Vostra.Result;

/// <summary>LINQ query-syntax support over <see cref="Task{TResult}"/> of <see cref="Result{T}"/>.</summary>
public static class ResultLinqAsyncExtensions
{
    /// <summary>Async LINQ <c>from … from …</c> over a <c>Task&lt;Result&lt;T&gt;&gt;</c> source with an async binder.</summary>
    public static async Task<Result<TResult>> SelectMany<T, TMid, TResult>(
        this Task<Result<T>> source,
        Func<T, Task<Result<TMid>>> bind,
        Func<T, TMid, TResult> project)
    {
        var result = await source.ConfigureAwait(false);
        if (result.IsError)
        {
            return result.ToError<TResult>();
        }

        var value = result.UnsafeValue;
        var mid = await bind(value).ConfigureAwait(false);
        return mid.Map(m => project(value, m));
    }
}
