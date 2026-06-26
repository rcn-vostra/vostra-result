using System.Diagnostics.CodeAnalysis;

namespace Vostra.Result;

public readonly partial struct Result<T>
{
    /// <summary>Runs <paramref name="onOk"/> on the value or <paramref name="onErr"/> on the errors.</summary>
    public TOut Match<TOut>(Func<T, TOut> onOk, Func<IReadOnlyList<ErrorBase>, TOut> onErr) =>
        IsError ? onErr(Errors) : onOk(UnsafeValue);

    /// <summary>Like <see cref="Match{TOut}"/>, but the error branch receives only the first error.</summary>
    public TOut MatchFirst<TOut>(Func<T, TOut> onOk, Func<ErrorBase, TOut> onFirstError) =>
        IsError ? onFirstError(FirstError) : onOk(UnsafeValue);

    /// <summary>Runs the matching action.</summary>
    public void Switch(Action<T> onOk, Action<IReadOnlyList<ErrorBase>> onErr)
    {
        if (IsError)
        {
            onErr(Errors);
        }
        else
        {
            onOk(UnsafeValue);
        }
    }

    /// <summary>Like <see cref="Switch"/>, but the error branch receives only the first error.</summary>
    public void SwitchFirst(Action<T> onOk, Action<ErrorBase> onFirstError)
    {
        if (IsError)
        {
            onFirstError(FirstError);
        }
        else
        {
            onOk(UnsafeValue);
        }
    }

    /// <summary>Gets the value when this is a success.</summary>
    public bool TryGetValue([MaybeNullWhen(false)] out T value)
    {
        if (IsSuccess)
        {
            value = UnsafeValue;
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>Gets the errors when this is a failure.</summary>
    public bool TryGetErrors([NotNullWhen(true)] out IReadOnlyList<ErrorBase>? errors)
    {
        if (IsError)
        {
            errors = Errors;
            return true;
        }

        errors = null;
        return false;
    }

    /// <summary>Returns the value, or <paramref name="fallback"/> when this is a failure.</summary>
    public T GetValueOr(T fallback) => IsSuccess ? UnsafeValue : fallback;

    /// <summary>Returns the value, or the result of <paramref name="fallback"/> when this is a failure.</summary>
    public T GetValueOr(Func<IReadOnlyList<ErrorBase>, T> fallback) => IsSuccess ? UnsafeValue : fallback(Errors);

    /// <summary>
    /// Returns the value, or throws <see cref="InvalidOperationException"/> when this is a failure. Use only
    /// when success is already established (e.g. after <see cref="TryGetValue"/>, or in a test). For a real
    /// fallback use <see cref="GetValueOr(T)"/>; to handle both branches use <see cref="Match{TOut}"/>.
    /// </summary>
    public T GetValueOrThrow() =>
        IsSuccess ? UnsafeValue : throw new InvalidOperationException($"Result is a failure ({FirstError}); there is no value.");
}
