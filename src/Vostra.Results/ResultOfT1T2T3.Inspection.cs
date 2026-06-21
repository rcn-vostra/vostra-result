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
            return onErr(Errors);
        }

        return _index switch
        {
            1 => onT1(_value1!),
            2 => onT2(_value2!),
            _ => onT3(_value3!),
        };
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
            onErr(Errors);
            return;
        }

        switch (_index)
        {
            case 1:
                onT1(_value1!);
                return;
            case 2:
                onT2(_value2!);
                return;
            default:
                onT3(_value3!);
                return;
        }
    }
}
