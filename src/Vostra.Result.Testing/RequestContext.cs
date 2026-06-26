namespace Vostra.Result.Testing;

/// <summary>Describes the operation that produced a failed result — attached to the failing error so
/// assertion failure messages can show what was attempted. Diagnostic only; carries no behavior.</summary>
/// <param name="Operation">What was attempted — an HTTP verb ("GET"), a queue action ("SEND"), etc.</param>
/// <param name="Target">Where — a request URL, a queue/topic name, an endpoint.</param>
/// <param name="Body">The payload sent, if any.</param>
public sealed record RequestContext(string Operation, string Target, object? Body);
