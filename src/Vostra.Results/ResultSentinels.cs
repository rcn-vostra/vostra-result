namespace Vostra.Results;

/// <summary>Shared singletons so the uninitialized-result error allocates once, not per generic instantiation.</summary>
internal static class ResultSentinels
{
    internal static readonly Error Uninitialized =
        new UnexpectedError(
            "A Result was used while uninitialized (default(Result)).",
            code: "Result.Uninitialized");

    internal static readonly Error[] UninitializedArray = { Uninitialized };

    internal static readonly IReadOnlyList<Error> UninitializedList = UninitializedArray;
}
