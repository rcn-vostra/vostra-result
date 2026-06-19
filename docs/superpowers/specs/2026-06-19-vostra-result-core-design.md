# Design — `Vostra.Results` Core (v1)

**Date:** 2026-06-19 · **Status:** Approved (brainstorming) · **Scope:** `Core` package only.

Source requirements: [result-type-requirements.md](../../../cc/docs/requirements/result-type-requirements.md).
This spec covers the **`Core`** package only; `AspNetCore` and `Testing` get their own spec → plan → build
cycles afterward (build order `Core → AspNetCore → Testing`).

---

## 1. Goal

A lean, dependency-free `Result<T>` for .NET where the **happy path is implicit**, the **failure path is
typed**, **async and sync chain identically**, and there is **no `.Value` footgun**. Judged by call-site
ergonomics, not internal cleverness — so usage-style tests are a first-class deliverable.

## 2. Scope (v1 = requirements "scope 2")

**In:** the two result types, the `Error` hierarchy, implicit conversions, `Match`/`Switch`, the synchronous
combinators, the `Task`-based async matrix, LINQ query syntax, and aggregation.

**Deferred** (later specs): `ValueTask` overloads (FR-6.1), early-exit `Scope`/`OrReturn` (FR-8 / OD-3),
serialization converters (FR-12), and `Created`-vs-`Ok` success tagging (FR-3.4 — it is an HTTP concern and
lands with `AspNetCore`). All HTTP status mapping is out of `Core` entirely.

## 3. Decisions carried in

- `Result<T>` in namespace `Vostra.Results` (OD-1).
- Single error, list-capable (OD-2).
- `Core` knows only a **neutral `ErrorType` taxonomy**; *all* HTTP status mapping lives in the future
  `AspNetCore` package. `Core` has zero HTTP awareness.
- No custom awaiter on `Result<T>` (FR-6.3): the canonical async shape is documented `await … .Then(…)`.

## 4. The two types

### `readonly struct Result<T>`
- Holds **either** a `T` **or** one-or-more `Error`s (single error by default; list only via aggregation).
- Success path allocates nothing (NFR-2). Implemented with a `T? _value`, an `Error[]? _errors`, and an
  `bool _initialized` discriminator.
- `bool IsSuccess` / `bool IsError`. **No public `Value`** (FR-1.3, kills P1/`default!`).
- Value access only via FR-4 members (below).
- Value-based equality and `ToString()` (FR-1.4).
- **`default(Result<T>)` is treated as a faulted result** — it yields a synthetic `UnexpectedError`
  ("uninitialized result"), never a success carrying `default(T)`. Deliberate improvement over ErrorOr,
  whose `default` is a silent success. (`_initialized == false ⇒ IsError`.)

### `readonly struct Result` (non-generic)
- Success/failure only, for void-returning operations. Same discriminator/footgun rules.

### Construction (FR-3)
- Implicit `T → Result<T>` (success); implicit `Error → Result<T>` and `Error[]`/`List<Error> → Result<T>`
  (failure). Same for the non-generic `Result` from `Error`(s).
- A single discoverable entry point for the rare explicit case (no `Result` vs `ResultStatus` split — fixes
  P3). Explicit factories only where implicit is ambiguous.

## 5. Error model (FR-2)

```
abstract Error {
    string  Code;          // stable identity, e.g. "Order.NotFound"
    string  Message;
    ErrorType Type;        // neutral taxonomy (NOT http)
    Exception? CausedBy;                            // opt-in (FR-2.3, fixes P10)
    IReadOnlyDictionary<string, object?>? Metadata; // opt-in
}
  ├ ValidationError    (ErrorType.Validation)
  ├ NotFoundError      (ErrorType.NotFound)
  ├ ConflictError      (ErrorType.Conflict)   ├ AlreadyExistsError (ErrorType.Conflict)
  ├ UnauthorizedError  (ErrorType.Unauthorized)
  ├ ForbiddenError     (ErrorType.Forbidden)
  └ UnexpectedError    (ErrorType.Unexpected)
```

`enum ErrorType { Validation, NotFound, Conflict, Unauthorized, Forbidden, Unexpected, Failure }`.

- Consumers extend by subclassing `Error` and choosing a `Type` (FR-2.2 extensibility).
- Value equality on `Code` + `Type` + `Message` (metadata compared structurally when present; `CausedBy`
  compared by reference) so tests can assert error **identity** (FR-1.4, groundwork for FR-11.3).
- Re-wrapping / aggregating preserves concrete type, `Code`, and `CausedBy` (FR-2.4, fixes P11).
- `Error` is a reference type; it allocates only on the failure path, which NFR-2 permits.

## 6. Combinator surface

| Group | Members |
|---|---|
| **Inspect (FR-4)** | `Match` / `MatchFirst`, `Switch` / `SwitchFirst`, `TryGetValue(out T)`, `TryGetErrors(out IReadOnlyList<Error>)`, `GetValueOr(fallback)` |
| **Sync (FR-5)** | `Map` (T→U), `Then`/`Bind` (T→Result\<U>), `Tap`, `TapError`, `Ensure(predicate, error)` defaulting to a `Validation` error (FR-5.4, fixes P8), `MapError` |
| **Async (FR-6)** | For every combinator: overloads for receiver ∈ {`Result<T>`, `Task<Result<T>>`} × projection ∈ {sync, `Task`}. Uniform names (overloaded `Then`, never `ThenAsync`). Any async-touching overload returns `Task<Result<U>>`, so a chain "lifts" to `Task<Result<>>` on its first async step and stays there — one `await` at the front, sync and async steps interleave with no ceremony. |
| **LINQ (FR-7)** | `Select`, `SelectMany` (sync + async overloads), `Where` — `from … from … where … select …` over `Result<T>` and `Task<Result<T>>`. `Where` failure produces a `Validation` error. No `where T : notnull` constraint unless required (FR-7.2). |
| **Aggregate (FR-9)** | `Result.Combine(params Result[])` / `Combine<T>(...)` accumulating errors; a `Result<IReadOnlyList<T>>` collector; throttled `SelectAsync(maxConcurrency)` ported from the reference repo's `CollectionsExtensions` (FR-9.2). |

## 7. Async ergonomics (the key UX)

The first async step lifts the chain into `Task<Result<>>`; it stays there, so every later step — sync or
async — is written exactly as in a pure-sync chain, with a single leading `await` and no `.Result` anywhere
(fixes P7/P12):

```csharp
Result<ShippingLabel> label = await GetOrderAsync(id)
    .Ensure(o => o.IsPaid, new ValidationError("Not paid"))  // sync, mid-async-chain
    .Then(o => BuildManifest(o))                             // sync  T -> Result
    .Then(m => _carrier.RegisterAsync(m))                    // async T -> Task<Result>
    .Tap(l => _log.Info(l.Tracking))                         // sync side-effect
    .MapError(e => e.Prefix("shipping"));
```

## 8. Project layout

```
Vostra.Results.sln
Directory.Build.props            # net8.0;net9.0; Nullable enable; LangVersion latest; GenerateDocumentationFile
src/Vostra.Results/              # Core — references ONLY the BCL (NFR-1)
tests/Vostra.Results.Tests/      # xUnit + FluentAssertions
LICENSE                          # MIT
THIRD-PARTY-NOTICES.md           # credit ErrorOr (Amichai Mantinband) + FluentResults (Michael Altmann) (§7)
```

- **Target frameworks:** `net8.0;net9.0` (NFR-3). `netstandard2.0` deferred (not needed for the reference-repo
  migration).
- **Nullability/AOT:** full nullable annotations; no reflection in the core type (NFR-4).

## 9. Testing approach

Tests are written as **usage scenarios**, not internal-state pokes, so they double as living documentation of
the ergonomics (per project working agreement). Coverage targets the call-site shapes in §7 and:

- produce-by-implicit-conversion (value and error) and consume-by-`Match` with no `.Value`;
- `default(Result<T>)` is a faulted result, not a success;
- `Ensure`/`Where` failure carries `ErrorType.Validation`;
- a deliberately **mixed sync/async** `Then` chain compiles and runs with one leading `await`;
- LINQ `from … from … select` over both `Result<T>` and `Task<Result<T>>`;
- `Combine` accumulates errors; `SelectAsync` respects the concurrency cap;
- error identity asserts on concrete type + `Code` (not message substrings).
- ≥90% coverage on `Core` (NFR-7).

## 10. Acceptance (subset of requirements §9 applicable to Core)

1. A method can `return value;` or `return new NotFoundError(...)` with no factory (§9.1 / FR-3).
2. No code path reads an invalid `.Value`; all consumption is `Match`/`Switch`/`TryGet` (§9.2 / FR-4).
3. A failed `Ensure`/`Where` yields a `Validation` error, not `Unexpected` (§9.3 / FR-5.4 / P8).
4. A mixed sync/async `.Then` chain compiles with one leading `await`, no `.Result` (§9.6 / FR-6 / P7).
5. `Core` references only the BCL (§9.8 / NFR-1).
6. (Deferred to NFR benchmark task) zero allocations on a 3-step happy-path chain (§9.7 / NFR-2).

## 11. Out of scope for this spec

HTTP/`ProblemDetails` mapping, the test HTTP client, `ValueTask` matrix, early-exit `Scope`, serialization,
`Created` tagging, benchmarking harness. Each is picked up in its own cycle.
