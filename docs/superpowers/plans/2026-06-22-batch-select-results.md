# SelectResultsAsync Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add `SelectResultsAsync` — a throttled async batch traverse that preserves every per-item `Result<T>` (the non-collapsing sibling of `Combine`) — and refactor `SelectAsync` to delegate to it.

**Architecture:** One new extension method on the existing `ResultCollectionExtensions` static class in Core. It reuses the proven `SemaphoreSlim` throttle from `SelectAsync` but returns the raw `IReadOnlyList<Result<TOut>>` instead of collapsing via `Combine`. `SelectAsync` then becomes a one-liner that calls the new method and appends `.Combine()`.

**Tech Stack:** C# (`net8.0` + `net9.0`), xUnit, FluentAssertions. Zero new dependencies.

**Spec:** [docs/superpowers/specs/2026-06-22-batch-select-results-design.md](../specs/2026-06-22-batch-select-results-design.md)

## Global Constraints

- **Zero runtime dependencies** in Core; AOT/trim-safe (generics + `SemaphoreSlim` only, no reflection).
- **0-warning bar** — build must stay clean.
- **Multi-target** `net8.0` + `net9.0`; `dotnet test` runs both TFMs.
- **No `.Value` footgun** — never surface a value outside `Match`/`Switch`/`TryGet`.
- **Commit messages must contain NO Claude/Anthropic attribution** (no `Co-Authored-By` Claude trailer, nothing AI-related). Per CLAUDE.md.
- **Quirk:** if `dotnet test` says "Unable to find a project to restore", run `git restore Vostra.Result.sln` (the IDE intermittently strips project entries).
- Branch: `feature/maximo-feedback-transport-neutral` (already checked out).

---

### Task 1: Add `SelectResultsAsync` and refactor `SelectAsync`

**Files:**
- Modify: `src/Vostra.Result/ResultAggregate.cs` (add method to `ResultCollectionExtensions`; refactor `SelectAsync` at lines 44-71)
- Test: `tests/Vostra.Result.Tests/AggregationTests.cs` (append new `[Fact]`s)

**Interfaces:**
- Consumes: existing `IEnumerable<Result<T>>.Combine()` extension (`ResultAggregate.cs:30`); `Result<T>` members `IsSuccess`/`IsError`/`Errors`/`FirstError`/`Match`; implicit `T → Result<T>` and `ErrorBase → Result<T>` conversions.
- Produces:
  ```csharp
  // on static class Vostra.Result.ResultCollectionExtensions
  public static Task<IReadOnlyList<Result<TOut>>> SelectResultsAsync<TIn, TOut>(
      this IEnumerable<TIn> source,
      Func<TIn, Task<Result<TOut>>> selector,
      int maxConcurrency,
      CancellationToken cancellationToken = default);
  ```

- [ ] **Step 1: Write the first failing test** — append to `tests/Vostra.Result.Tests/AggregationTests.cs` (inside the `AggregationTests` class):

```csharp
[Fact]
public async Task SelectResultsAsync_preserves_both_successes_and_failures()
{
    var items = new[] { 1, 2, 3, 4 };

    IReadOnlyList<Result<int>> outcomes = await items.SelectResultsAsync(
        i => Task.FromResult(i % 2 == 0
            ? (Result<int>)i
            : (Result<int>)new ValidationError($"odd {i}")),
        maxConcurrency: 2);

    outcomes.Should().HaveCount(4);
    outcomes.Count(r => r.IsSuccess).Should().Be(2);
    outcomes.Count(r => r.IsError).Should().Be(2);
    outcomes[1].Match(v => v, _ => -1).Should().Be(2);   // input order preserved
    outcomes[0].IsError.Should().BeTrue();
}
```

- [ ] **Step 2: Run the test, verify it fails to compile**

Run: `dotnet test --filter "FullyQualifiedName~SelectResultsAsync_preserves" 2>&1 | tail -n 20`
Expected: FAIL — compile error, `'IEnumerable<int>' does not contain a definition for 'SelectResultsAsync'`.

- [ ] **Step 3: Implement `SelectResultsAsync`** — in `src/Vostra.Result/ResultAggregate.cs`, add this method to `ResultCollectionExtensions` (place it directly above the existing `SelectAsync`, keeping the existing `// NOTE:` comment which applies to both):

```csharp
/// <summary>
/// Projects each item through <paramref name="selector"/> with at most
/// <paramref name="maxConcurrency"/> concurrent invocations, returning every per-item
/// <see cref="Result{TOut}"/> in source order — successes and failures preserved verbatim
/// (no combining). Use this when you need each outcome; use <see cref="SelectAsync{TIn,TOut}"/>
/// when you want a single combined result.
/// </summary>
/// <remarks>
/// Cancellation is observed at the throttle gate; in-flight selector calls run to completion
/// unless the selector itself observes the token.
/// </remarks>
public static async Task<IReadOnlyList<Result<TOut>>> SelectResultsAsync<TIn, TOut>(
    this IEnumerable<TIn> source,
    Func<TIn, Task<Result<TOut>>> selector,
    int maxConcurrency,
    CancellationToken cancellationToken = default)
{
    if (maxConcurrency < 1)
    {
        throw new ArgumentOutOfRangeException(nameof(maxConcurrency), "Must be at least 1.");
    }

    using var gate = new SemaphoreSlim(maxConcurrency);

    var tasks = source.Select(async item =>
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await selector(item).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }).ToArray();

    return await Task.WhenAll(tasks).ConfigureAwait(false);
}
```

- [ ] **Step 4: Run the test, verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~SelectResultsAsync_preserves" 2>&1 | tail -n 15`
Expected: PASS (both TFMs).

- [ ] **Step 5: Add the remaining behavior tests** — append these `[Fact]`s to `AggregationTests`:

```csharp
[Fact]
public async Task SelectResultsAsync_returns_results_in_source_order()
{
    var items = Enumerable.Range(0, 6).ToArray();

    // Later items finish first; result order must still match input order.
    IReadOnlyList<Result<int>> outcomes = await items.SelectResultsAsync(
        async i => { await Task.Delay((6 - i) * 5); return (Result<int>)i; },
        maxConcurrency: 6);

    outcomes.Select(r => r.Match(v => v, _ => -1)).Should().Equal(0, 1, 2, 3, 4, 5);
}

[Fact]
public async Task SelectResultsAsync_respects_the_concurrency_cap()
{
    var current = 0;
    var peak = 0;
    var gate = new object();

    async Task<Result<int>> Track(int i)
    {
        lock (gate) { current++; peak = Math.Max(peak, current); }
        await Task.Delay(20);
        lock (gate) { current--; }
        return i;
    }

    await Enumerable.Range(0, 8).SelectResultsAsync(Track, maxConcurrency: 3);

    peak.Should().BeLessThanOrEqualTo(3);
}

[Fact]
public async Task SelectResultsAsync_throws_when_maxConcurrency_below_one()
{
    Func<Task> act = () => Enumerable.Range(0, 3)
        .SelectResultsAsync(i => Task.FromResult<Result<int>>(i), maxConcurrency: 0);

    await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
}

[Fact]
public async Task SelectResultsAsync_returns_empty_for_empty_source()
{
    IReadOnlyList<Result<int>> outcomes = await Array.Empty<int>()
        .SelectResultsAsync(i => Task.FromResult<Result<int>>(i), maxConcurrency: 2);

    outcomes.Should().BeEmpty();
}

[Fact]
public async Task SelectResultsAsync_observes_cancellation()
{
    using var cts = new CancellationTokenSource();
    cts.Cancel();

    Func<Task> act = () => Enumerable.Range(0, 3)
        .SelectResultsAsync(i => Task.FromResult<Result<int>>(i), maxConcurrency: 2, cts.Token);

    await act.Should().ThrowAsync<OperationCanceledException>();
}

[Fact]
public async Task SelectResultsAsync_outcomes_zip_back_to_inputs_in_order()
{
    var items = new[] { 1, 2, 3 };

    IReadOnlyList<Result<int>> outcomes = await items
        .SelectResultsAsync(i => Task.FromResult<Result<int>>(i * 10), maxConcurrency: 2);

    var paired = items.Zip(outcomes, (item, outcome) => (item, value: outcome.Match(v => v, _ => -1)));
    paired.Should().Equal((1, 10), (2, 20), (3, 30));
}
```

- [ ] **Step 6: Run all the new tests, verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~SelectResultsAsync" 2>&1 | tail -n 15`
Expected: PASS — 7 tests × 2 TFMs.

- [ ] **Step 7: Refactor `SelectAsync` to delegate, and pin the collapse contrast** — replace the existing `SelectAsync` body (`src/Vostra.Result/ResultAggregate.cs:44-71`) with the delegating one-liner:

```csharp
/// <summary>
/// Projects each item through <paramref name="selector"/> with at most
/// <paramref name="maxConcurrency"/> concurrent invocations, then combines the results
/// (all values, or all errors accumulated). For the per-item outcomes instead of a combined
/// result, use <see cref="SelectResultsAsync{TIn,TOut}"/>.
/// </summary>
public static async Task<Result<IReadOnlyList<TOut>>> SelectAsync<TIn, TOut>(
    this IEnumerable<TIn> source,
    Func<TIn, Task<Result<TOut>>> selector,
    int maxConcurrency) =>
    (await source.SelectResultsAsync(selector, maxConcurrency).ConfigureAwait(false)).Combine();
```

Then append this regression test to `AggregationTests` (documents that `SelectAsync` *still* collapses — discards successes on any failure — which is exactly why `SelectResultsAsync` exists):

```csharp
[Fact]
public async Task SelectAsync_still_collapses_to_errors_discarding_successes()
{
    var items = new[] { 1, 2, 3 };

    Result<IReadOnlyList<int>> result = await items.SelectAsync(
        i => Task.FromResult(i == 2
            ? (Result<int>)new NotFoundError("no 2")
            : (Result<int>)i),
        maxConcurrency: 2);

    result.IsError.Should().BeTrue();
    result.Errors.Should().HaveCount(1);   // the two successes (1, 3) are gone
}
```

- [ ] **Step 8: Run the full test suite on both TFMs, verify green and 0 warnings**

Run: `dotnet test 2>&1 | tail -n 25`
Expected: PASS — all existing tests + 8 new tests, both `net8.0` and `net9.0`, 0 warnings. (If "Unable to find a project to restore": `git restore Vostra.Result.sln` then re-run.)

- [ ] **Step 9: Commit**

```bash
git add src/Vostra.Result/ResultAggregate.cs tests/Vostra.Result.Tests/AggregationTests.cs
git commit -m "feat(core): add SelectResultsAsync (non-collapsing batch traverse)

Preserves every per-item Result<T> in source order with a throttle and
optional CancellationToken; SelectAsync now delegates to it and appends
Combine. Closes Maximo adapter team feedback ask #2."
```

---

### Task 2: Document `SelectResultsAsync` in the usage guide

**Files:**
- Modify: `docs/usage.md` (the "Combine many / validate-all" section, around lines 126-136)

**Interfaces:**
- Consumes: the `SelectResultsAsync` signature shipped in Task 1.

- [ ] **Step 1: Add a "keep every outcome" subsection** — in `docs/usage.md`, immediately after the existing "Combine many / validate-all" code block (the one ending with the `SelectAsync` throttled fan-out line, ~line 136), insert:

```markdown
### Keep every outcome (don't collapse)

`Combine`/`SelectAsync` are *all-or-nothing*: one failure discards every success. When you instead need
to **record every per-item outcome** — the successes *and* the failures — and decide once over the whole
batch, reach for `SelectResultsAsync`. It's the non-collapsing sibling of `Combine`: same throttle, but it
hands back every `Result<T>` in input order so you summarize however you like.

```csharp
// run each item (throttled), keep every per-item Result<T> — successes AND failures
IReadOnlyList<Result<Handled>> outcomes =
    await batch.SelectResultsAsync(HandleAsync, maxConcurrency: 4, ct);

var failures = outcomes.Where(r => r.IsError).ToArray();   // nothing discarded
bool accept  = failures.Length == 0;

// pair back to inputs when the outcome doesn't carry identity (order is guaranteed):
foreach (var (item, outcome) in batch.Zip(outcomes))
    outcome.Switch(ok => Audit(item, ok), errs => Audit(item, errs[0].Code));
```

`SelectAsync` is `SelectResultsAsync(...).Combine()` — use it when you want the single combined result;
use `SelectResultsAsync` when the per-item detail is the point.
```

- [ ] **Step 2: Verify the doc renders and references are correct**

Read back the edited section of `docs/usage.md` and confirm: the code fence is balanced, the signature matches Task 1 (`SelectResultsAsync(selector, maxConcurrency, ct)`), and no `.Value` access appears.

- [ ] **Step 3: Commit**

```bash
git add docs/usage.md
git commit -m "docs: document SelectResultsAsync in the usage guide"
```

---

## Self-Review

**Spec coverage:**
- New method signature (spec §2) → Task 1 Step 3. ✓
- Order guarantee (§2.1) → Task 1 Step 5 (`returns_results_in_source_order`, `zip_back_to_inputs`). ✓
- Throttle + `maxConcurrency<1` throws (§2.1) → Step 5 (`respects_the_concurrency_cap`, `throws_when_maxConcurrency_below_one`). ✓
- Cancellation (§2.1) → Step 5 (`observes_cancellation`). ✓
- Empty source (§2.1) → Step 5 (`returns_empty_for_empty_source`). ✓
- `SelectAsync` delegates, signature unchanged (§2.2) → Task 1 Step 7. ✓
- Preserves both successes and failures (§1, §5) → Step 1 + Step 7 contrast test. ✓
- Exception convention unchanged (§2.1) → existing `// NOTE` comment retained (Step 3); not re-tested (out of scope per spec §7). ✓
- Usage doc update (user-requested addition) → Task 2. ✓
- Rejected: `ItemResult` pairing, `BatchOutcome`, DSL, `IAsyncEnumerable` (§3) → not implemented. ✓

**Placeholder scan:** No TBD/TODO; every code step shows complete code. ✓

**Type consistency:** `SelectResultsAsync<TIn,TOut>(IEnumerable<TIn>, Func<TIn,Task<Result<TOut>>>, int, CancellationToken)` used identically in spec, Task 1 implementation, all tests, the `SelectAsync` delegation, and the usage doc. ✓
