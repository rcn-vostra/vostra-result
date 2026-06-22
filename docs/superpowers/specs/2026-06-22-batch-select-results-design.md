# Design — `SelectResultsAsync`: a non-collapsing batch traverse

**Date:** 2026-06-22 · **Status:** Approved (brainstorm) · **Scope:** Core (`Vostra.Results`) only.
**Origin:** Maximo adapter team feedback, ask #2 ([cc/docs-in/request-for-changes-wom-masa.md](../../../cc/docs-in/request-for-changes-wom-masa.md)).
**Second opinions:** internal max-depth subagent + ChatGPT — both converged on this shape
([question](../../../cc/docs-out/2026-06-22-batch-outcome-combinator-question.md),
[ChatGPT reply](../../../cc/docs-in/2026-06-22-batch-outcome-combinator-question-chatgpt-feedback.md)).

---

## 1. Problem

A consumer processes a **batch** of items, runs a fallible async operation on each, and must **record
every per-item outcome** — heterogeneous successes *and* heterogeneous failures — before making **one**
aggregate decision over the whole batch.

The existing aggregation helpers can't express this because they **collapse**:

- `Result.Combine(...)` / `IEnumerable<Result<T>>.Combine()` — on any failure, returns *all errors* but
  **discards every success**.
- `SelectAsync(selector, maxConcurrency)` is `Combine` under the hood (`ResultAggregate.cs:69-70`) — same
  all-or-nothing collapse into `Result<IReadOnlyList<T>>`.

There is no combinator that runs each item and hands back **every per-item `Result<T>` intact**, so the
caller can summarize however it needs. That is the gap this design closes.

The *kinds* of success (e.g. skipped vs applied) and failure (business-rejection vs external-refusal vs
transient) are **domain concepts** the consumer encodes in their own `T` and their own `ErrorBase`
subtypes / `ErrorType`. The library's only job is to **preserve each item's `Result<T>` verbatim** — it
does not model the consumer's taxonomy.

## 2. Decision

Add **one** extension method to `ResultCollectionExtensions` — the non-collapsing sibling of `Combine`:

```csharp
/// <summary>
/// Projects each item through <paramref name="selector"/> with at most
/// <paramref name="maxConcurrency"/> concurrent invocations, returning every per-item
/// <see cref="Result{TOut}"/> in source order — successes and failures preserved verbatim
/// (no combining). Use this when you need each outcome; use <see cref="SelectAsync"/> when you
/// want a single combined result.
/// </summary>
/// <remarks>
/// <paramref name="selector"/> must return failures as <c>Result</c> values, not throw; a thrown
/// exception propagates out of <see cref="Task.WhenAll"/> and bypasses the errors-as-values contract.
/// Cancellation is observed at the throttle gate; in-flight selector calls run to completion unless the
/// selector itself observes the token.
/// </remarks>
public static async Task<IReadOnlyList<Result<TOut>>> SelectResultsAsync<TIn, TOut>(
    this IEnumerable<TIn> source,
    Func<TIn, Task<Result<TOut>>> selector,
    int maxConcurrency,
    CancellationToken cancellationToken = default);
```

### 2.1 Semantics

- **Order:** results are returned in **source order** (guaranteed — the task array is built in source
  order, like `SelectAsync`). This guarantee is documented so callers can safely `source.Zip(outcomes)`.
- **Throttle:** at most `maxConcurrency` concurrent `selector` invocations, via `SemaphoreSlim` (reused
  from the existing `SelectAsync` pattern). `maxConcurrency < 1` throws `ArgumentOutOfRangeException`.
- **Cancellation:** `cancellationToken` is passed to `gate.WaitAsync(cancellationToken)` so scheduling
  stops promptly on cancel. The `selector` signature is unchanged; a consumer that needs per-call
  cancellation closes over the token.
- **Exceptions:** same convention as `SelectAsync` — the `selector` should return failures as `Result`
  values; a thrown exception propagates. (Wrapping thrown exceptions into error values is a separate,
  pre-existing hardening item — see `cc/plans/2026-06-22-nuget-and-prerelease-hardening.md` §5.4 — and is
  intentionally **out of scope** here; whatever is decided there applies to both methods.)
- **Empty source:** returns an empty list.

### 2.2 Refactor `SelectAsync` to delegate

`SelectAsync` becomes a thin collapse over the new primitive, removing the duplicated throttle body:

```csharp
public static async Task<Result<IReadOnlyList<TOut>>> SelectAsync<TIn, TOut>(
    this IEnumerable<TIn> source,
    Func<TIn, Task<Result<TOut>>> selector,
    int maxConcurrency) =>
    (await source.SelectResultsAsync(selector, maxConcurrency).ConfigureAwait(false)).Combine();
```

`SelectAsync`'s public signature is **unchanged** (no `CancellationToken` added — that remains a separate
hardening decision so we don't widen an existing public API as a side effect here).

## 3. Rejected alternatives (and why)

- **A `BatchOutcome<T>` summary type** (pre-partitioned `Successes`/`Failures`/`AllSucceeded`/
  `ToCombined()`). Rejected for v1: every member is a one-line LINQ fold over the returned list, its
  `ToCombined()` duplicates the existing `Combine`, and a `Successes` collection of `T` would surface
  values **outside** a `Match` — re-opening the `.Value` footgun the core type is built to forbid (DP3,
  FR-1.3). Build it only if multiple consumers demonstrably need it.
- **A fold/reduce DSL** for bucketing outcomes. Rejected: it's the explicit §4 non-goal ("not a general
  functional-programming library"); LINQ (`Where`/`GroupBy`/`Aggregate`) already serves this.
- **Input-pairing as the return shape** (`IReadOnlyList<ItemResult<TItem,TOut>>` or a tuple). Rejected as
  the primary/only shape: a paired type can't be the primitive `SelectAsync` delegates to, it breaks the
  `Combine`/`SelectAsync` family model, and pairing is recoverable in one line via `source.Zip(outcomes)`
  (safe because input order is guaranteed). Identity also commonly already lives in `TOut` and/or the
  error. `ItemResult<,>` may be added later if real demand appears; it is not in this design.
- **`IAsyncEnumerable<Result<T>>` streaming.** Rejected as the default: the use case fully materializes
  before deciding, and streaming complicates throttle/ordering/cancellation/disposal for no benefit here.
  A separate streaming variant can be added later on proven demand.

## 4. Usage (call site)

The per-item op returns failures as values (never throws); successes carry their own domain flavour:

```csharp
public enum Disposition { Skipped, Applied }
public sealed record ItemHandled(Guid ItemId, Disposition Disposition);

async Task<Result<ItemHandled>> HandleAsync(WorkItem item)
{
    if (item.AlreadyDone)  return new ItemHandled(item.Id, Disposition.Skipped); // success flavour A
    if (!item.IsValid)     return new ValidationError($"Item {item.Id} invalid.", "Item.Invalid");
    var applied = await _downstream.ApplyAsync(item);
    return applied.Match(
        ok:  _    => new ItemHandled(item.Id, Disposition.Applied),              // success flavour B
        err: errs => (Result<ItemHandled>)errs.ToArray());                       // typed failures preserved
}

// Fan out (throttled); every per-item outcome comes back intact, in order.
IReadOnlyList<Result<ItemHandled>> outcomes =
    await batch.SelectResultsAsync(HandleAsync, maxConcurrency: 4, ct);

// One aggregate decision — plain LINQ, the caller's domain logic:
var failures = outcomes.Where(r => r.IsError).ToArray();
bool accept  = failures.Length == 0;

// Summarize by domain category if needed (GroupBy over the caller's own error types/codes):
var byKind = failures.GroupBy(f => f.FirstError.Code).ToDictionary(g => g.Key, g => g.Count());

// Pair back to inputs when the outcome doesn't already carry identity (order is guaranteed):
foreach (var (item, outcome) in batch.Zip(outcomes))
    outcome.Switch(ok => _audit.Record(item.Id, ok.Disposition), err => _audit.Record(item.Id, err[0].Code));
```

No `.Value` anywhere — every value is reached inside `Match`/`Switch`.

## 5. Testing (usage-first)

Tests must demonstrate the call-site shape, not just unit-poke internals:

- **Preserves both:** mixed batch (some succeed, some fail) → list keeps every success *and* every
  failure (contrast with `SelectAsync`, which would discard the successes).
- **Order guarantee:** results align with input order under concurrency.
- **Throttle:** never exceeds `maxConcurrency` concurrent invocations (instrument the selector).
- **Validation:** `maxConcurrency < 1` throws `ArgumentOutOfRangeException`.
- **Empty source:** returns empty list.
- **Cancellation:** cancelling before/within scheduling stops further selector invocations.
- **Delegation parity:** `SelectAsync` still returns the same combined result as before the refactor
  (regression guard on the existing behavior).
- **`Zip` pairing:** documented pattern works and aligns outcomes to inputs.

## 6. Constraints honored

Zero runtime deps; AOT/trim-safe (generics + `SemaphoreSlim`, no reflection); happy-path allocation
unchanged vs. `SelectAsync` (an aggregation helper that allocates its result list is acceptable, as
`Combine` already does); no `.Value` footgun introduced; one method + zero new public types added.

## 7. Out of scope (tracked elsewhere)

- Ask #1 (transport-neutral chain-and-assert package split) — separate spec.
- Exception-to-error wrapping for `selector` and adding `CancellationToken` to `SelectAsync` —
  hardening plan §5.4.
- `ItemResult<,>` pairing type and `IAsyncEnumerable` streaming — deferred pending demand.
