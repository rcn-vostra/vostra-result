namespace Vostra.Results.Testing;

/// <summary>Thrown when a Vostra result assertion fails. Prefixed to avoid an ambiguous-reference
/// clash with NUnit's <c>AssertionException</c>.</summary>
public sealed class VostraAssertionException : Exception
{
    /// <summary>Creates the exception with a diagnostic message.</summary>
    public VostraAssertionException(string message) : base(message) { }
}
