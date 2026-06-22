# Design question — a "preserve every per-item outcome" batch combinator

*Self-contained: written so a reader with no access to the repo (e.g. ChatGPT) can give a useful
second opinion. For an internal reader, the library is `Vostra.Results`.*

---

## 0. What we want from you

We are weighing whether to add **one new combinator** to a small .NET `Result<T>` library. Please answer
three things:

- **(a) Solution critique / alternatives.** Is our proposed API the right shape — or is there a cleaner
  or more idiomatic solution we're missing (including "you don't need a new API at all")?
- **(b) Scope / identity check.** Are we sacrificing the library's focus to satisfy one user? Is
  "preserve every per-item outcome across a batch" a *generic enough* problem that a general-purpose
  `Result` library should own it — or does it belong in user code?
- **(c) A concrete call-site example.** Show how the chosen approach reads in C#, so we can judge it by
  ergonomics, not prose.

---

## 1. What the library is (just enough context)

`Vostra.Results` is a lean, dependency-free, async-friendly `Result<T>` type for .NET. The opinion baked
in: **the happy path is implicit, failure is a typed value, and there is no property that hands you a
value when the operation failed.**

- **Types.** `Result` (success/failure, no value), `Result<T>` (value or error), and multi-success
  unions `Result<T1,T2>` / `Result<T1,T2,T3>` ("this success shape, or that one, or an error").
- **`Result<T>` is a `readonly struct`** — zero allocation on the happy path. There is **no `.Value`**;
  you extract via `Match` / `Switch` / `TryGetValue` / `GetValueOr`. "Forgot to check success" can't compile.
- **Error model: single error, list-capable.** The struct holds one error by default; aggregation
  produces a list only when needed. An error is a typed value (`ErrorBase`) with a stable `Code`, a human
  `Message`, and a neutral `ErrorType` category (Validation/NotFound/Conflict/Unauthorized/.../Unexpected).
- **Composition.** A full sync + async combinator matrix: `Map` (T→U), `Then`/`Bind` (T→Result<U>),
  `Tap`/`TapError`, `Ensure` (guard→validation error), `MapError`. The chain short-circuits on the first
  error and "lifts" to `Task<Result<T>>` on the first async step, so sync and async steps interleave with
  a single `await` at the front. LINQ query syntax is also supported.
- **Aggregation (the relevant part).** Two existing helpers:
  - `Result.Combine(r1, r2, …)` → succeeds with all values, **or fails with ALL errors accumulated**.
    This is the "tell me every bad field at once" validator pattern.
  - `items.SelectAsync(i => SaveAsync(i), maxConcurrency: n)` → a **throttled** async fan-out that runs a
    fallible op over a collection and **then `Combine`s** the results into one `Result<IReadOnlyList<T>>`.

**Explicit non-goal (v1):** this is *not* a general functional-programming library — no `Option`,
`Either`, or validators DSL. We add a combinator only when it earns its place.

---

## 2. The problem (generalized from a real consumer)

A consumer processes a **batch of items**, running a fallible (usually async) operation on each, and must
**record every per-item outcome** before making **one aggregate decision** over the whole batch.

Crucially, the per-item outcomes are *heterogeneous and all meaningful*:

- some items are legitimate **no-ops** (skipped — a kind of success),
- some **applied** and advanced state (a different kind of success),
- some were **rejected by business rules** (one kind of failure),
- some were **refused by an external system** (another kind of failure),
- some **failed transiently** (a third kind of failure).

Only after collecting *all* of these does the consumer decide a single accept-or-reject for the batch, and
it needs the per-item detail to build that decision (and to report it).

### Why the existing tools don't fit

- **`Combine`** collapses: on any failure you get *all errors* but **lose every success**, and you lose
  which input produced which outcome. It also erases the *distinctions among successes*.
- **`SelectAsync`** is `Combine` under the hood — same collapse: `Result<IReadOnlyList<T>>` is all-or-nothing;
  a single failure discards the successful values and the per-item failures alike.

The gap: there is **no combinator that runs each item and hands back every per-item `Result<T>` intact**,
preserving both the success *and* the failure detail, so the caller can summarize however it needs.

Note on "kinds of success/failure": those flavours (no-op vs applied; rejected vs refused vs transient)
are **domain concepts**. The consumer encodes them in their own `T` (an enum/record/union) and their own
`ErrorBase` subtypes / `ErrorType`. So the library's job is only to **preserve each item's `Result<T>`
verbatim** — not to model the consumer's taxonomy.

---

## 3. The three options we're choosing between

### Option A — Thin: don't collapse

A traverse-style async combinator (throttled, like `SelectAsync`) that runs each item and returns the full
list of per-item results, **without** combining:

```csharp
// returns every per-item outcome, in input order, paired with its input
Task<IReadOnlyList<(TItem Item, Result<TOut> Outcome)>>
    SelectEach<TItem, TOut>(this IEnumerable<TItem> items,
                            Func<TItem, Task<Result<TOut>>> op,
                            int maxConcurrency = …);
```

The caller folds/summarizes themselves (LINQ over the list). Smallest surface; most flexible; matches
"preserve the full per-item outcome." Sync and non-paired overloads as needed.

### Option B — Thin + a summary value type

Same traverse, but also return a small `BatchOutcome<T>` that pre-partitions successes/failures and offers
conveniences (`AllSucceeded`, `Successes`, `Failures`, counts, `ToCombined()`), so "then decide one
accept/reject" is a one-liner. More convenience; more public surface to lock in for v1.

### Option C — Full fold/reduce DSL

Traverse plus a fluent reducer for bucketing outcomes by category. Powerful but speculative — drifts toward
"a general FP library," which our non-goals explicitly rule out.

**Our current lean is A** (possibly with the input-pairing overload): the honest minimum that solves the
problem without speculative surface. The `BatchOutcome` of B is easy for a consumer to write themselves and
hard for us to un-ship once it's public.

---

## 4. The questions again (please address each)

**(a)** Critique A/B/C. Is there a better/other solution — e.g. is this just `Task.WhenAll` + existing
combinators in disguise? Should it be `IAsyncEnumerable<Result<T>>` (streaming) instead of a materialized
list? Is pairing with the input the right call, or should the op return a richer outcome? Is the name wrong?

**(b)** Is this generic enough to belong in the library, or are we contorting a focused library for one
consumer? Consider that `Combine` (collapsing) already exists and this is essentially its
**non-collapsing sibling** — does that symmetry justify it, or does it signal scope creep given our
"not a general FP library" non-goal?

**(c)** Show the **call-site** code for your recommended approach: defining the per-item op, calling the
combinator, and producing the single aggregate accept/reject decision from the preserved outcomes.

---

## 5. Constraints any answer should respect

- **Zero runtime dependencies** in core; AOT/trim-safe; no reflection.
- Happy path stays allocation-light (`readonly struct` discipline); an aggregation helper that allocates
  the result list is acceptable (`Combine` already does).
- **Async-first**, with a throttle (`maxConcurrency`) — the consumer serializes real I/O.
- Must not introduce a `.Value`-style footgun or a way to read a value that wasn't produced.
- Prefer a **small, uniform** surface over a powerful-but-large one. YAGNI.
