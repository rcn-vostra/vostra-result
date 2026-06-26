namespace Vostra.Result;

public readonly partial struct Result<T>
{
    /// <summary>LINQ <c>select</c>. Equivalent to <see cref="Map{U}(Func{T,U})"/>.</summary>
    public Result<TResult> Select<TResult>(Func<T, TResult> selector) => Map(selector);

    /// <summary>LINQ <c>from … from …</c>. Binds then projects; short-circuits on the first error.</summary>
    public Result<TResult> SelectMany<TMid, TResult>(
        Func<T, Result<TMid>> bind,
        Func<T, TMid, TResult> project)
    {
        if (IsError)
        {
            return ToError<TResult>();
        }

        var value = UnsafeValue;
        return bind(value).Map(mid => project(value, mid));
    }

    /// <summary>LINQ <c>where</c>. A false predicate yields a <see cref="ValidationError"/>.</summary>
    public Result<T> Where(Func<T, bool> predicate) =>
        Ensure(predicate, new ValidationError("The 'where' predicate was not satisfied.", code: "General.Validation"));
}
