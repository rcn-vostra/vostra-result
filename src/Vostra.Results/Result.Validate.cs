namespace Vostra.Results;

public readonly partial struct Result
{
    /// <summary>
    /// Accumulating validation guard. Passes the result through unchanged when <paramref name="condition"/>
    /// is <c>true</c>; otherwise adds a <see cref="ValidationError"/> built from <paramref name="errorMessage"/>
    /// (and the optional <paramref name="field"/> / <paramref name="code"/>). Unlike <c>Ensure</c> this does
    /// <b>not</b> short-circuit — chain several to collect <i>every</i> failure at once (the classic "tell me
    /// all the bad fields"), then map to an RFC 7807 field-map via the AspNetCore package.
    /// </summary>
    /// <remarks>
    /// The <paramref name="condition"/> is evaluated eagerly (it is a plain <c>bool</c>, not a deferred
    /// predicate), so use this for <i>independent</i> checks. For a guard that depends on a previous one
    /// passing — e.g. a null-check followed by a dereference — use <c>Ensure</c> (deferred) or an early return.
    /// </remarks>
    /// <param name="condition">The check; the result passes when this is <c>true</c>.</param>
    /// <param name="errorMessage">The validation message used when the check fails.</param>
    /// <param name="field">Optional input field name — drives the AspNetCore validation field-map.</param>
    /// <param name="code">Optional stable error code; defaults to the <see cref="ValidationError"/> fallback.</param>
    public Result Validate(bool condition, string errorMessage, string? field = null, string? code = null)
    {
        if (condition)
        {
            return this;
        }

        var error = code is null
            ? new ValidationError(errorMessage, field: field)
            : new ValidationError(errorMessage, field: field, code: code);

        return Combine(this, error);
    }
}
