namespace Vostra.Result;

public readonly partial struct Result
{
    /// <summary>Runs <paramref name="onOk"/> on success or <paramref name="onErr"/> on the errors.</summary>
    public TOut Match<TOut>(Func<TOut> onOk, Func<IReadOnlyList<ErrorBase>, TOut> onErr) =>
        IsError ? onErr(Errors) : onOk();

    /// <summary>Like <see cref="Match{TOut}"/>, but the error branch receives only the first error.</summary>
    public TOut MatchFirst<TOut>(Func<TOut> onOk, Func<ErrorBase, TOut> onFirstError) =>
        IsError ? onFirstError(FirstError) : onOk();

    /// <summary>Runs the matching action.</summary>
    public void Switch(Action onOk, Action<IReadOnlyList<ErrorBase>> onErr)
    {
        if (IsError) { onErr(Errors); } else { onOk(); }
    }

    /// <summary>Like <see cref="Switch"/>, but the error branch receives only the first error.</summary>
    public void SwitchFirst(Action onOk, Action<ErrorBase> onFirstError)
    {
        if (IsError) { onFirstError(FirstError); } else { onOk(); }
    }
}
