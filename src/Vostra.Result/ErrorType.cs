namespace Vostra.Result;

/// <summary>
/// Transport-neutral taxonomy of failure kinds. Carries no HTTP semantics —
/// status mapping lives in the Vostra.Result.AspNetCore package.
/// </summary>
public enum ErrorType
{
    /// <summary>An unexpected fault (e.g. wrapping an exception).</summary>
    Unexpected,

    /// <summary>Input failed validation.</summary>
    Validation,

    /// <summary>A requested resource was not found.</summary>
    NotFound,

    /// <summary>The request conflicts with current state.</summary>
    Conflict,

    /// <summary>Authentication is required or failed.</summary>
    Unauthorized,

    /// <summary>The caller is authenticated but not permitted.</summary>
    Forbidden,
}
