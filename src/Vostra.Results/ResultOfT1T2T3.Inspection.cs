namespace Vostra.Results;

public readonly partial struct Result<T1, T2, T3>
{
    /// <summary>Runs the function matching the active arm, or <paramref name="onErr"/> on the errors.</summary>
    public TOut Match<TOut>(
        Func<T1, TOut> onT1,
        Func<T2, TOut> onT2,
        Func<T3, TOut> onT3,
        Func<IReadOnlyList<ErrorBase>, TOut> onErr)
    {
        if (IsError)
        {
            ArgumentNullException.ThrowIfNull(onErr);
            return onErr(Errors);
        }

        switch (_index)
        {
            case 1:
                ArgumentNullException.ThrowIfNull(onT1);
                return onT1(_value1!);
            case 2:
                ArgumentNullException.ThrowIfNull(onT2);
                return onT2(_value2!);
            default:
                ArgumentNullException.ThrowIfNull(onT3);
                return onT3(_value3!);
        }
    }

    /// <summary>Runs the action matching the active arm, or <paramref name="onErr"/> on the errors.</summary>
    public void Switch(
        Action<T1> onT1,
        Action<T2> onT2,
        Action<T3> onT3,
        Action<IReadOnlyList<ErrorBase>> onErr)
    {
        if (IsError)
        {
            ArgumentNullException.ThrowIfNull(onErr);
            onErr(Errors);
            return;
        }

        switch (_index)
        {
            case 1:
                ArgumentNullException.ThrowIfNull(onT1);
                onT1(_value1!);
                return;
            case 2:
                ArgumentNullException.ThrowIfNull(onT2);
                onT2(_value2!);
                return;
            default:
                ArgumentNullException.ThrowIfNull(onT3);
                onT3(_value3!);
                return;
        }
    }
}
