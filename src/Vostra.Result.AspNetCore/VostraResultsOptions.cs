namespace Vostra.Result.AspNetCore;

/// <summary>
/// Configuration for HTTP mapping. Override the status for a whole <see cref="ErrorType"/>
/// or for an individual error <c>Code</c>. Resolution precedence: code override → type map → default.
/// </summary>
public sealed class VostraResultsOptions
{
    private readonly Dictionary<ErrorType, int> _byType = new();
    private readonly Dictionary<string, int> _byCode = new(StringComparer.Ordinal);

    /// <summary>A shared, unconfigured instance using only the built-in defaults.</summary>
    public static VostraResultsOptions Default { get; } = new();

    /// <summary>Overrides the HTTP status for a whole <see cref="ErrorType"/>. Returns this for chaining.</summary>
    public VostraResultsOptions MapStatus(ErrorType type, int status)
    {
        _byType[type] = status;
        return this;
    }

    /// <summary>Overrides the HTTP status for a single error <c>Code</c>. Returns this for chaining.</summary>
    public VostraResultsOptions MapStatusForCode(string code, int status)
    {
        _byCode[code] = status;
        return this;
    }

    /// <summary>Resolves the status for an error applying the documented precedence.</summary>
    internal int ResolveStatus(ErrorBase error)
    {
        if (_byCode.TryGetValue(error.Code, out var byCode))
        {
            return byCode;
        }

        return _byType.TryGetValue(error.Type, out var byType) ? byType : DefaultStatusMap.ForType(error.Type);
    }
}
