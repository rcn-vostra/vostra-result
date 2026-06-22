Recommendation

Add Option A, but name it as the non-collapsing sibling of SelectAsync.

I would not add BatchOutcome<T> yet. I would definitely not add a fold/reduce DSL.

Your library’s stated shape is lean, dependency-free, typed-error, no .Value, composition-focused, and already includes Combine / async matrix support. The docs explicitly position Combine as the “many independent results → one result” helper, with accumulated errors but collapsed success/failure state. A non-collapsing traversal is a natural missing counterpart.

(a) Critique
Option A — best fit

This is the right minimum:

Task<IReadOnlyList<(TItem Item, Result<TOut> Outcome)>> SelectEachAsync(...)

It does one generic thing:

run the operation for every item, preserve every Result<T> exactly as produced.

That belongs close to SelectAsync, because the difference is not domain-specific. It is the difference between:

// collapse
items.SelectAsync(SaveAsync) -> Result<IReadOnlyList<Saved>>

// preserve
items.SelectEachAsync(SaveAsync) -> IReadOnlyList<Result<Saved>>

That is clean symmetry.

Naming

I would avoid plain SelectEach. In .NET, async methods should normally end in Async.

Good candidates:

SelectEachAsync
SelectResultsAsync
TraverseResultsAsync

My pick: SelectResultsAsync or SelectEachAsync.

SelectEachAsync reads nicely, but does not strongly say “non-collapsing”.
SelectResultsAsync is clearer: it returns the results, not the values.

Example:

var outcomes = await items.SelectResultsAsync(ProcessAsync, maxConcurrency: 8);

That is fairly obvious.

Pairing with input

Yes, include an input-pairing overload. This is important.

Without pairing, callers either need to close over item identity in TOut / error metadata, or zip results back to inputs and hope ordering is guaranteed. Since your method already promises input order, pairing is a cheap ergonomic win.

I would expose the paired version as the primary one:

Task<IReadOnlyList<ItemResult<TItem, TOut>>> SelectResultsAsync<TItem, TOut>(
    this IEnumerable<TItem> items,
    Func<TItem, Task<Result<TOut>>> selector,
    int maxConcurrency = 4,
    CancellationToken cancellationToken = default);

I would consider a tiny readonly record struct instead of tuple:

public readonly record struct ItemResult<TItem, TOut>(
    TItem Item,
    Result<TOut> Outcome);

Reason: public tuple element names are weak API documentation. ItemResult<TItem,TOut> gives you a stable concept without becoming BatchOutcome<T>. It is still thin.

If you want absolute minimum surface, tuple is acceptable. But I think ItemResult earns its place.

Streaming / IAsyncEnumerable

Do not make streaming the primary API.

A streaming variant is useful, but it complicates throttling, ordering, exception behavior, cancellation, and disposal. Your consumer wants “collect all outcomes, then decide”. A materialized list is the correct default.

You can add streaming later if there is proven demand:

IAsyncEnumerable<ItemResult<TItem, TOut>> SelectResultsStreamingAsync(...)

Do not start there.

Task.WhenAll?

No, this is not “just Task.WhenAll”.

Task.WhenAll gives you fan-out, but not:

throttling
stable result ordering
consistent Result<T> preservation convention
a library-standard sibling to SelectAsync
a natural place to enforce cancellation / exception-to-error policy if you have one

A user can write it, yes. But the same is true for Combine.

Option B — wait

BatchOutcome<T> is useful, but too opinionated for v1.

The moment you add:

Successes
Failures
AllSucceeded
ToCombined()

you start making decisions about what “successes” and “failures” mean structurally, how to expose errors, whether to retain item identity, whether partial success is a first-class state, etc.

That is not necessary. Let consumers write:

var failures = outcomes.Where(x => !x.Outcome.IsSuccess).ToList();

Or use Match.

Option C — no

That is scope creep. It pushes the library toward a validation/fold DSL. Not needed.

(b) Scope / identity check

Yes, this is generic enough.

The key argument is symmetry:

Combine = preserve all errors, discard successes if any failure
SelectAsync = run many, then Combine
SelectResultsAsync = run many, preserve every per-item Result

This does not model batch business semantics. It does not introduce PartialSuccess, Skipped, Rejected, retry policy, or categorization. It only preserves the existing abstraction instead of collapsing it.

That is inside the library’s identity.

But keep it thin. The library should own traversal + throttling + preservation, not batch interpretation.

Suggested API

I would add this:

public readonly record struct ItemResult<TItem, TOut>(
    TItem Item,
    Result<TOut> Outcome);

public static Task<IReadOnlyList<ItemResult<TItem, TOut>>> SelectResultsAsync<TItem, TOut>(
    this IEnumerable<TItem> items,
    Func<TItem, Task<Result<TOut>>> selector,
    int maxConcurrency = 4,
    CancellationToken cancellationToken = default);

Optional non-paired overload:

public static Task<IReadOnlyList<Result<TOut>>> SelectResultsAsync<TItem, TOut>(
    this IEnumerable<TItem> items,
    Func<TItem, Task<Result<TOut>>> selector,
    int maxConcurrency = 4,
    CancellationToken cancellationToken = default);

But if you only add one: add the paired version.

(c) Call-site example
public enum ItemAction
{
    Skipped,
    Applied
}

public sealed record ItemOutcome(
    Guid ItemId,
    ItemAction Action,
    string? ExternalReference = null);

public sealed class ExternalRefusedError : ErrorBase
{
    public ExternalRefusedError(Guid itemId, string reason)
        : base(
            code: "Item.ExternalRefused",
            message: $"External system refused item {itemId}: {reason}",
            errorType: ErrorType.Conflict)
    {
    }

    protected override ErrorBase CloneWith(
        string code,
        string message,
        Exception? causedBy,
        IReadOnlyDictionary<string, object?>? metadata)
        => new ExternalRefusedError(Guid.Empty, message);
}

Per-item operation:

async Task<Result<ItemOutcome>> ProcessItemAsync(ImportItem item)
{
    if (item.AlreadyProcessed)
        return new ItemOutcome(item.Id, ItemAction.Skipped);

    if (!item.IsValid)
        return new ValidationError($"Item {item.Id} is invalid.", "Item.Invalid");

    var external = await externalClient.ApplyAsync(item);

    if (external.Refused)
        return new ExternalRefusedError(item.Id, external.Reason);

    if (external.TransientFailure)
        return new Error($"Transient failure for item {item.Id}.", "Item.TransientFailure");

    return new ItemOutcome(
        item.Id,
        ItemAction.Applied,
        external.Reference);
}

Call-site:

IReadOnlyList<ItemResult<ImportItem, ItemOutcome>> outcomes =
    await items.SelectResultsAsync(ProcessItemAsync, maxConcurrency: 8, cancellationToken);

Aggregate decision:

var failures = outcomes
    .Where(x => !x.Outcome.IsSuccess)
    .ToList();

var applied = outcomes
    .Where(x => x.Outcome.Match(
        onOk: outcome => outcome.Action == ItemAction.Applied,
        onErr: _ => false))
    .ToList();

var skipped = outcomes
    .Where(x => x.Outcome.Match(
        onOk: outcome => outcome.Action == ItemAction.Skipped,
        onErr: _ => false))
    .ToList();

Result<BatchReport> batchResult =
    failures.Count == 0
        ? new BatchReport(
            Accepted: true,
            AppliedCount: applied.Count,
            SkippedCount: skipped.Count,
            FailedCount: 0)
        : new BatchRejectedError(
            message: $"Batch rejected. {failures.Count} item(s) failed.");

If the report must include every detail:

var report = new BatchReport(
    Accepted: failures.Count == 0,
    AppliedCount: applied.Count,
    SkippedCount: skipped.Count,
    FailedCount: failures.Count,
    Items: outcomes.Select(x => x.Outcome.Match(
        onOk: ok => new BatchItemReport(
            ItemId: x.Item.Id,
            Status: ok.Action.ToString(),
            ErrorCode: null,
            Message: null),
        onErr: errors => new BatchItemReport(
            ItemId: x.Item.Id,
            Status: "Failed",
            ErrorCode: errors[0].Code,
            Message: errors[0].Message)
    )).ToList());

Result<BatchReport> final =
    failures.Count == 0
        ? report
        : new BatchRejectedError("Batch rejected.");
Bottom line

Add A, preferably as:

SelectResultsAsync

returning:

IReadOnlyList<ItemResult<TItem, TOut>>

Do not add BatchOutcome<T> yet. The library should preserve per-item outcomes; the caller should interpret them. That keeps the API generic, small, and consistent with the existing Result philosophy.