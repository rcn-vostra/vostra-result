namespace Vostra.Result;

/// <summary>Shared singletons so the uninitialized-result error allocates once, not per generic instantiation.</summary>
internal static class ResultSentinels
{
    internal static readonly ErrorBase Uninitialized =
        new Error(
            "A Result was used while uninitialized (default(Result)).",
            code: "Result.Uninitialized");

    internal static readonly ErrorBase[] UninitializedArray = { Uninitialized };

    internal static readonly IReadOnlyList<ErrorBase> UninitializedList = UninitializedArray;
}
