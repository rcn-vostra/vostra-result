namespace Vostra.Result;

/// <summary>Conversions for <see cref="Result{T}"/>.</summary>
public static class ResultExtensions
{
    /// <summary>
    /// Collapses a <c>Result&lt;T&gt;</c> to a valueless <see cref="Result"/> — discards the value but keeps
    /// the verdict, the success kind (so a <c>Created</c> stays a 201), and any errors. Use when the caller
    /// only needs "did it work?", not the value.
    /// </summary>
    public static Result ToResult<T>(this Result<T> result) =>
        result.IsError
            ? Result.FromErrors(result.ErrorArray)
            : result.SuccessKind == SuccessKind.Created ? Result.Created() : Result.Ok();
}
