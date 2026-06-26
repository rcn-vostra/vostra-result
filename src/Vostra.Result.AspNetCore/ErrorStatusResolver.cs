namespace Vostra.Result.AspNetCore;

/// <summary>Resolves an <see cref="ErrorBase"/> to an HTTP status using the supplied options.</summary>
public static class ErrorStatusResolver
{
    /// <summary>Resolves the HTTP status for <paramref name="error"/> (code override → type map → default).</summary>
    public static int Resolve(ErrorBase error, VostraResultsOptions options) => options.ResolveStatus(error);
}
