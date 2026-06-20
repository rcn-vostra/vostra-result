namespace Vostra.Results.AspNetCore;

/// <summary>
/// The built-in <see cref="ErrorType"/> → HTTP status map. This is the shared contract the
/// Testing package consumes to rebuild typed errors from the wire (do not let it drift).
/// </summary>
public static class DefaultStatusMap
{
    private static readonly IReadOnlyDictionary<ErrorType, int> _defaults = new Dictionary<ErrorType, int>
    {
        [ErrorType.Validation] = 400,
        [ErrorType.Unauthorized] = 401,
        [ErrorType.Forbidden] = 403,
        [ErrorType.NotFound] = 404,
        [ErrorType.Conflict] = 409,
        [ErrorType.Unexpected] = 500,
    };

    /// <summary>The default status for each <see cref="ErrorType"/>.</summary>
    public static IReadOnlyDictionary<ErrorType, int> Defaults => _defaults;

    /// <summary>The default HTTP status for an <see cref="ErrorType"/>; 500 for anything unmapped.</summary>
    public static int ForType(ErrorType type) => _defaults.TryGetValue(type, out var status) ? status : 500;
}
