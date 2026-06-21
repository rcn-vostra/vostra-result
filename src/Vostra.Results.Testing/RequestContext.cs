namespace Vostra.Results.Testing;

/// <summary>The request that produced a failed result, attached to the failing error's metadata
/// under the key <c>"request"</c> and surfaced in assertion failure messages.</summary>
/// <param name="Verb">The HTTP verb (e.g. "GET").</param>
/// <param name="Url">The resolved request URL.</param>
/// <param name="Body">The request body, if any.</param>
public sealed record RequestContext(string Verb, string Url, object? Body);
