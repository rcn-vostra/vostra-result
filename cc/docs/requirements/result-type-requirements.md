# Requirements — A Lean, Async-Friendly `Result` Type (`vostra-result`)

**Status:** Draft for review · **Audience:** the `vostra-result` library project.

> **Reference repository (source of truth for "today"):**
> `C:\Users\Robert\source\repos\_ARCHIVE\VCC\popcat-assortment-admin-api`
> Every `file:line` link below points into that repo so an agent building this library can open the
> exact existing code being preserved or replaced. The companion doc `result-pattern.md` (same folder)
> walks the existing implementation end-to-end.

> Goal in one sentence: replace the FluentResults-based `AM.Extensions` layer (in the reference repo)
> with a small, dependency-free, allocation-light, fully-async `Result` type that makes the
> *happy path implicit*, the *failure path typed*, and the *boundary code (HTTP, tests) trivial* —
> and release it back to the community under MIT.

Companion docs (same folder):
- [result-pattern.md](result-pattern.md) — how the existing pattern works end-to-end.

---

## 1. Context & Motivation

Today the reference solution uses [FluentResults](https://github.com/altmann/FluentResults) wrapped by
a hand-rolled extension layer in `AM.Extensions`. The design is sound (errors-as-values, typed reasons,
a single HTTP-mapping point), but daily use surfaced concrete ergonomic friction. We already pay a
maintenance cost on the wrapper *and* carry a third-party dependency whose "reasons list + metadata"
model is heavier than we use. We want to consolidate that into one purpose-built type we fully own and
can name as we like (`Result<T>`, namespace e.g. `Vostra.Results`).

---

## 2. Current State — What We Have

### 2.1 Strengths to preserve (non-negotiable)

| # | Strength | Where it lives today |
|---|----------|----------------------|
| S1 | Errors-as-values; services return `Result<T>`, never throw for expected failures | `AM.ProductDetails.Core/Services/*` |
| S2 | Typed failure/success *kinds* drive behavior, not strings | [ResultExtensions/](C:/Users/Robert/source/repos/_ARCHIVE/VCC/popcat-assortment-admin-api/Modules/AM.Extensions/AM.Extensions/ResultExtensions/) |
| S3 | **Single** place maps kind → HTTP status | [ApiControllerBase.cs:85](C:/Users/Robert/source/repos/_ARCHIVE/VCC/popcat-assortment-admin-api/Modules/AM.Extensions/AM.Extensions.AspNetCore/Controller/ApiControllerBase.cs#L85) |
| S4 | Layering: core Result project depends on **nothing** but the result lib | [AM.Extensions.csproj](C:/Users/Robert/source/repos/_ARCHIVE/VCC/popcat-assortment-admin-api/Modules/AM.Extensions/AM.Extensions/AM.Extensions.csproj) |
| S5 | Test client collapses HTTP into `Result<T>` → tests read as domain scripts | [IntegrationTestHelper.cs](C:/Users/Robert/source/repos/_ARCHIVE/VCC/popcat-assortment-admin-api/Modules/AM.ProductDetails/test/AM.ProductDetails.IntegrationTests/Helpers/IntegrationTestHelper.cs) |
| S6 | Rich, diagnostic failure messages (verb, URL, request body, server error) | [IntegrationTestHelper.cs:75-116](C:/Users/Robert/source/repos/_ARCHIVE/VCC/popcat-assortment-admin-api/Modules/AM.ProductDetails/test/AM.ProductDetails.IntegrationTests/Helpers/IntegrationTestHelper.cs#L75) |
| S7 | Strict deserialization guard with self-explaining hint | [IntegrationTestHelper.cs:122-139](C:/Users/Robert/source/repos/_ARCHIVE/VCC/popcat-assortment-admin-api/Modules/AM.ProductDetails/test/AM.ProductDetails.IntegrationTests/Helpers/IntegrationTestHelper.cs#L122) |
| S8 | Chainable `.Assert()` that returns the result | [FluentResultExtensions.cs:9](C:/Users/Robert/source/repos/_ARCHIVE/VCC/popcat-assortment-admin-api/Modules/AM.ProductDetails/test/AM.ProductDetails.IntegrationTests/Helpers/FluentResultExtensions.cs#L9) |

### 2.2 Pain points to fix (the reason for this project)

| # | Pain | Evidence today | Target requirement |
|---|------|----------------|--------------------|
| P1 | Must check `IsSuccess` then read `.Value` — no exhaustive matching; `Deconstruct` returns `default!` | [ResultExtensions.cs:12](C:/Users/Robert/source/repos/_ARCHIVE/VCC/popcat-assortment-admin-api/Modules/AM.Extensions/AM.Extensions/ResultExtensions/ResultExtensions.cs#L12) | FR-4 (`Match`) |
| P2 | No way to assert on error *identity*; tests substring-match messages | `*.ErrorsStr().Should().Contain("...")` throughout `TestProducts.cs` | FR-2, FR-11 |
| P3 | Confusing construction: `Result.Ok` vs `ResultStatus.Ok` vs `Result.Fail` | [ResultStatus.cs](C:/Users/Robert/source/repos/_ARCHIVE/VCC/popcat-assortment-admin-api/Modules/AM.Extensions/AM.Extensions/ResultExtensions/ResultStatus.cs) | FR-3 (implicit conversions) |
| P4 | `SelectMany` surface incomplete (only `Task<Result<T>>` receiver, no `Select`/`Where`) | [ResultSelectExtensions.cs](C:/Users/Robert/source/repos/_ARCHIVE/VCC/popcat-assortment-admin-api/Modules/AM.Extensions/AM.Extensions/ResultExtensions/ResultSelectExtensions.cs) | FR-7 |
| P5 | No early-exit / `?`-operator feel; imperative flow is awkward | LINQ-only today | FR-8 |
| P6 | HTTP mapping is a central `switch` that must be edited per new error kind | [ApiControllerBase.cs:85-95](C:/Users/Robert/source/repos/_ARCHIVE/VCC/popcat-assortment-admin-api/Modules/AM.Extensions/AM.Extensions.AspNetCore/Controller/ApiControllerBase.cs#L85) | FR-10 (status on error) |
| P7 | Async overload matrix is incomplete → clumsy at call sites | [ResultExtensions.cs:36-50](C:/Users/Robert/source/repos/_ARCHIVE/VCC/popcat-assortment-admin-api/Modules/AM.Extensions/AM.Extensions/ResultExtensions/ResultExtensions.cs#L36) | FR-6 |
| P8 | `ValidateThat` failure maps to **500**, not 400 (bug) | [ResultExtensions.cs:61-67](C:/Users/Robert/source/repos/_ARCHIVE/VCC/popcat-assortment-admin-api/Modules/AM.Extensions/AM.Extensions/ResultExtensions/ResultExtensions.cs#L61) | FR-5 |
| P9 | `ToResult` *always* fails regardless of input; misleading name | [ResultExtensions.cs:24](C:/Users/Robert/source/repos/_ARCHIVE/VCC/popcat-assortment-admin-api/Modules/AM.Extensions/AM.Extensions/ResultExtensions/ResultExtensions.cs#L24) | dropped / renamed |
| P10 | `OkResult`/`CreatedResult` carry an always-empty `message`/`metadata` | [OkResult.cs:5](C:/Users/Robert/source/repos/_ARCHIVE/VCC/popcat-assortment-admin-api/Modules/AM.Extensions/AM.Extensions/ResultExtensions/OkResult.cs#L5) | FR-2 (lean errors) |
| P11 | Error re-wrap loses type/`CausedBy`/metadata | [ResultStatus.cs:34](C:/Users/Robert/source/repos/_ARCHIVE/VCC/popcat-assortment-admin-api/Modules/AM.Extensions/AM.Extensions/ResultExtensions/ResultStatus.cs#L34) | FR-2, FR-9 |
| P12 | Sync-over-async `.Result` block | [IntegrationTestHelper.cs:77](C:/Users/Robert/source/repos/_ARCHIVE/VCC/popcat-assortment-admin-api/Modules/AM.ProductDetails/test/AM.ProductDetails.IntegrationTests/Helpers/IntegrationTestHelper.cs#L77) | FR-11 |
| P13 | Test client lives in one module; not shared | path under `AM.ProductDetails.IntegrationTests` | FR-11 |

---

## 3. Vision & Design Principles

- **DP1 — Happy path is implicit.** `return value;` and `return new NotFoundError("…");` both compile.
  No factory ceremony for the common case. *(borrow: ErrorOr implicit operators)*
- **DP2 — Failure path is typed and self-describing.** Every error carries a stable `Code` and an
  HTTP status; the *type* survives from service to HTTP wire to test assertion.
- **DP3 — The value only exists where it's valid.** Primary consumption is via `Match`/`Switch`; there
  is no public `.Value` that returns garbage on failure.
- **DP4 — Async is first-class, not bolted on.** Every combinator has the full `{sync, Task}` matrix so
  call sites never juggle `await` placement.
- **DP5 — Lean & owned.** Core type has **zero** runtime dependencies, is a `readonly struct`
  (no happy-path allocation), and is small enough to read in one sitting.
- **DP6 — Boundaries collapse into `Result`.** HTTP, tests, and (later) messaging boundaries map their
  transport outcome into `Result<T>` with rich diagnostics — preserving S5–S8.
- **DP7 — Open/closed mapping.** Adding an error kind never requires editing a central `switch`.

---

## 4. Goals & Non-Goals

**Goals**
- One package family: `Core` (the type), `AspNetCore` (HTTP mapping), `Testing` (test client + asserts).
- Drop the FluentResults dependency entirely.
- Public release under MIT with attribution to borrowed projects.

**Non-Goals (v1)**
- Not building a general functional-programming library (no `Option`, `Either`, validators DSL).
- Not supporting `IResult`-style multiple *successes* (the always-empty `OkResult` metadata is cut).
- Not solving distributed/transactional concerns — purely an in-process result-carrying type.
- Not replacing exceptions for *unexpected* faults (bugs, OOM) — those still throw.

---

## 5. Functional Requirements

> Signatures below are **illustrative, non-binding**. The type is named `Result<T>` (decision OD-1).

### FR-1 — Core type
- **FR-1.1** Provide `Result<T>` (carries a value or error(s)) and a non-generic `Result` (success/failure only).
- **FR-1.2** `Result<T>` MUST be a `readonly struct`; the success path MUST NOT allocate.
- **FR-1.3** Expose `bool IsSuccess` / `bool IsError`. MUST NOT expose a public `Value` that returns
  `default`/garbage on failure (kills P1/`default!`). Value access is via FR-4 only.
- **FR-1.4** Equality and `ToString()` defined (value-based) for testability.

### FR-2 — Error model
- **FR-2.1** Define an abstract error carrying at minimum: `Code` (stable string), `Message`, and an
  HTTP status (or a transport-neutral `ErrorKind` enum that maps to one). *(borrow: ErrorOr `ErrorType`+code; FluentResults typed `IError`)*
- **FR-2.2** Ship built-in kinds equivalent to today: `Validation` (400), `NotFound` (404),
  `Conflict`/`AlreadyExists` (409), `Unexpected`/`Failure` (500). Extensible by consumers.
- **FR-2.3** An error MAY carry an optional `Exception` (`CausedBy`) and an optional metadata bag —
  but these MUST be opt-in, not always-allocated (fixes P10).
- **FR-2.4** Re-wrapping/aggregating errors MUST preserve type, code, and `CausedBy` (fixes P11).

### FR-3 — Construction
- **FR-3.1** Implicit conversion `T → Result<T>` (success). *(borrow: ErrorOr)*
- **FR-3.2** Implicit conversion `Error → Result<T>` and `Error[]`/`List<Error> → Result<T>` (failure).
- **FR-3.3** Provide explicit factories only where implicit is ambiguous (e.g. `Result.Created(value)`
  to distinguish 201 from 200). A single discoverable entry point — no `Result` vs `ResultStatus`
  split (fixes P3).
- **FR-3.4** Distinguish **Created (201)** from **Ok (200)** without a separate success-reason object —
  e.g. a flag/enum on the success branch.

### FR-4 — Inspection & extraction (no footgun)
- **FR-4.1** `Match<TOut>(Func<T,TOut> onOk, Func<IReadOnlyList<Error>,TOut> onErr)`. *(borrow: ErrorOr/OneOf)*
- **FR-4.2** `Switch(Action<T> onOk, Action<IReadOnlyList<Error>> onErr)`.
- **FR-4.3** `MatchFirst` / `SwitchFirst` convenience for single-error consumers.
- **FR-4.4** Safe extraction helpers: `GetValueOr(fallback)`, `TryGetValue(out T value)`,
  `bool TryGetErrors(out IReadOnlyList<Error>)`. A debug-only `ValueOrThrow()` MAY exist for tests.

### FR-5 — Synchronous composition
- **FR-5.1** `Map` (functor: `T → U`). *(borrow: both)*
- **FR-5.2** `Then`/`Bind` (monadic: `T → Result<U>`).
- **FR-5.3** `Tap` (side-effect on success, pass-through) and `TapError`.
- **FR-5.4** `Ensure(predicate, error)` — **MUST** default to a `Validation` error (400), fixing P8.
  The error argument is a real typed `Error`, not a bare string.
- **FR-5.5** `MapError` to transform/categorize errors.

### FR-6 — Async matrix (first-class)
- **FR-6.1** Every FR-5 combinator MUST exist for the full matrix:
  receiver ∈ {`Result<T>`, `Task<Result<T>>`, `ValueTask<Result<T>>`} ×
  projection ∈ {sync, `Task`, `ValueTask`}.
- **FR-6.2** Naming MUST be uniform so call sites don't branch on sync/async (e.g. overloaded `Then`,
  not `Then` + `ThenAsync` + `BindAsync`). *(borrow: ErrorOr's `Then`/`ThenAsync` coverage; improve uniformity)*
- **FR-6.3** Provide an `await`-able `Result<T>` ergonomic where reasonable, or document the canonical
  `await … .Then(…)` shape. No sync-over-async anywhere in the lib (fixes P7/P12).

### FR-7 — LINQ query syntax
- **FR-7.1** Implement `Select`, `SelectMany` (sync + async overloads), and `Where` so `from … from …
  where … select …` works over `Result<T>` and `Task<Result<T>>` (completes P4).
- **FR-7.2** Relax the `where T : notnull` constraint unless strictly required.

### FR-8 — Early exit ("`?`-operator feel")
- **FR-8.1** Provide a scoped early-exit mechanism: inside a `Result.Scope(() => { … })` block, an
  `.OrReturn()` (working name) on a failed result short-circuits the whole scope and returns its errors.
- **FR-8.2** Implementation MAY use a private control-flow exception **confined to the lib**; it MUST
  NOT leak. Document the cost (failure path unwinds the stack) and advise against hot-loop use.
- **FR-8.3** This is **additive sugar** — LINQ (FR-7) and `Then` (FR-5/6) remain the primary styles.

### FR-9 — Aggregation
- **FR-9.1** Combine N results into one, accumulating errors: `Result.Combine(params Result[])` and a
  `Result<IReadOnlyList<T>>` collector. *(borrow: FluentResults `Merge`)*
- **FR-9.2** A throttled async projection equivalent to today's `SelectAsync(maxTasksAtATime)` — used to
  serialize per-`DbContext` work — MUST be available (port [CollectionsExtensions.cs:18](C:/Users/Robert/source/repos/_ARCHIVE/VCC/popcat-assortment-admin-api/Modules/AM.Extensions/AM.Extensions/CollectionsExtensions.cs#L18)).

### FR-10 — ASP.NET Core integration (separate package)
- **FR-10.1** `ToHttpResponse(result)` and collection/pagination overloads, equivalent to today
  ([ApiControllerBase.cs:14-33](C:/Users/Robert/source/repos/_ARCHIVE/VCC/popcat-assortment-admin-api/Modules/AM.Extensions/AM.Extensions.AspNetCore/Controller/ApiControllerBase.cs#L14)).
- **FR-10.2** Status code MUST be derived from the **error itself** (FR-2.1), not a central `switch`
  (fixes P6). Adding an error kind requires no change to the mapping code.
- **FR-10.3** Success path MUST distinguish 200 vs 201 (FR-3.4) and stamp the correlation/operation id.
- **FR-10.4** Error envelope MUST be RFC 7807 `ProblemDetails` (carry `code` + human message), so the
  error *identity* is on the wire and FR-11.3 can assert it (decision OD-4). A thin success envelope is
  retained only to carry `operationId` + `data`.

### FR-11 — Testing toolkit (separate package) — preserve & generalize S5–S8
- **FR-11.1** A `TestHttpClient`/helper returning `Result<T>` for GET/POST/PUT/DELETE, ported from
  [IntegrationTestHelper.cs](C:/Users/Robert/source/repos/_ARCHIVE/VCC/popcat-assortment-admin-api/Modules/AM.ProductDetails/test/AM.ProductDetails.IntegrationTests/Helpers/IntegrationTestHelper.cs),
  but **fully async** (no `.Result`, fixes P12) and **module-agnostic** (fixes P13).
- **FR-11.2** Preserve the rich failure message (verb, URL, request body, server error across envelope
  shapes) and the strict-deserialization guard with hint (S6/S7).
- **FR-11.3** On HTTP failure, the returned `Result` MUST be re-tagged with the **typed error** matching
  the status/`code` from the response — so tests assert `result.ShouldHaveError(ErrorCode.NotFound)`
  instead of substring-matching messages (fixes P2). This is the feature that makes the symmetry *real*.
- **FR-11.4** Provide chainable assertion helpers (`Assert`/`ShouldBeSuccess`/`ShouldHaveError`) that
  return the result for fluent chaining (port S8).
- **FR-11.5** Serializer (Newtonsoft vs System.Text.Json) and response-envelope shape MUST be
  injectable, not hard-coded.

### FR-12 — Serialization
- **FR-12.1** `Result<T>` and `Error` MUST serialize/deserialize cleanly under **System.Text.Json**
  (with optional Newtonsoft support) for the test round-trip and any logging.
- **FR-12.2** Enum-as-string for error kinds/codes by default.

---

## 6. Non-Functional Requirements

- **NFR-1 — Dependencies.** Core: **zero** runtime NuGet dependencies. AspNetCore: only ASP.NET Core
  abstractions.
  <!-- The original "Testing: only the chosen serializer + an assertions lib" clause was REMOVED on
       2026-06-21. The Testing package is an ASP.NET-Core-specific test client that parses the exact response
       contract emitted by Vostra.Results.AspNetCore, so it depends on that package directly (reusing the real
       envelopes/Pagination — zero duplication/drift). The shared framework it inherits ships with the .NET SDK
       and every real consumer already has it via WebApplicationFactory. See the Testing design spec §2
       (docs/superpowers/specs/2026-06-21-testing-design.md) for the full rationale and the B-fallback. -->
  Testing's dependencies are defined by that package's design spec, not constrained here.
- **NFR-2 — Allocation.** Success path zero-alloc (`readonly struct`); error path allocates only the
  error(s). Benchmarked with BenchmarkDotNet; a happy-path `Then` chain MUST not allocate.
- **NFR-3 — Target frameworks.** `net8.0` and `net10.0` (the two LTS releases; net9.0 STS dropped
  2026-06-22 on the .NET 10 upgrade). `netstandard2.0` SHOULD be considered for broad reuse.
- **NFR-4 — Nullability & AOT.** Full nullable annotations; trim/AOT-safe (no reflection in the core type).
- **NFR-5 — Thread-safety.** The value type is immutable and safe to pass across awaits.
- **NFR-6 — API stability.** SemVer; public surface documented with XML docs and analyzer-friendly.
- **NFR-7 — Test coverage.** ≥90% on Core; the LINQ, async matrix, and early-exit paths exhaustively tested.
- **NFR-8 — Packaging.** Three NuGet packages under one repo; source-linked; CI builds + benchmarks.

---

## 7. What We Borrow & Licensing

Both upstreams are **MIT** — copying with attribution is permitted; we will release under **MIT** and,
where we improve a shared concept, offer it back upstream.

| Concept | Borrow from | Notes |
|---------|-------------|-------|
| Implicit `T`/`Error` → result conversions | **ErrorOr** (MIT, Amichai Mantinband) | Core ergonomic win (DP1). |
| `Match`/`MatchFirst`/`Switch` | **ErrorOr** / OneOf | FR-4. |
| `Then`/`ThenAsync` async chaining shape | **ErrorOr** | FR-6 — we extend to a uniform full matrix. |
| Error type + `Code`/`ErrorType` enum | **ErrorOr** | FR-2. |
| Typed `IError` kinds, `CausedBy(exception)`, metadata, `Merge` | **FluentResults** (MIT, Michael Altmann) | FR-2.3, FR-9. |
| `readonly struct`, single-error leanness | original / ErrorOr-influenced | DP5, NFR-2. |
| LINQ `SelectMany`/`Select`/`Where` | language idiom + existing impl in reference repo | FR-7, supersedes [ResultSelectExtensions.cs](C:/Users/Robert/source/repos/_ARCHIVE/VCC/popcat-assortment-admin-api/Modules/AM.Extensions/AM.Extensions/ResultExtensions/ResultSelectExtensions.cs). |
| HTTP collapse + diagnostic test client | **reference repo** (S5–S8) | FR-10/FR-11, our own contribution to give back. |

**Attribution obligation:** retain MIT license headers/notices for any code lifted verbatim; credit
both projects in the README and THIRD-PARTY-NOTICES.

---

## 8. Migration (from `AM.Extensions` in the reference repo)

- **M1** New library consumed by `AM.Extensions`/`AM.Extensions.AspNetCore` as a drop-in, OR those
  projects are retired in favor of it. Decide in §10 (OD-6).
- **M2** Map current API → new API: `ResultStatus.X` → implicit conversions / `Result.Created`;
  `IsNotFound()` etc → `ShouldHaveError(code)` / `Match`; `Assert()` ported as-is.
- **M3** Service-layer `from … from … select` chains (e.g.
  [ProductDetailsService.cs:73](C:/Users/Robert/source/repos/_ARCHIVE/VCC/popcat-assortment-admin-api/Modules/AM.ProductDetails/AM.ProductDetails.Core/Services/ProductDetailsService.cs#L73))
  MUST keep compiling under the new LINQ surface (FR-7) — they are the migration's regression canary.
- **M4** Integration tests are the acceptance harness: the existing tests in
  `AM.ProductDetails.IntegrationTests` should pass after a mechanical rewrite to the new vocabulary,
  with substring assertions upgraded to typed-error assertions (FR-11.3).

---

## 9. Acceptance Criteria (Definition of Done)

1. A service method can `return value;` or `return new NotFoundError(...);` with no factory (FR-3).
2. No code path reads a `.Value` that can be invalid; all consumption is `Match`/`Switch`/`TryGet` (FR-4).
3. A failed `Ensure`/validation yields **400**, not 500 (FR-5.4 / P8).
4. Adding a new error kind changes **zero** lines in the HTTP mapping (FR-10.2 / P6).
5. An integration test asserts `ShouldHaveError(ErrorCode.NotFound)` with **no** message substring (FR-11.3 / P2).
6. The async matrix lets `await client.Post(...).Then(x => client.Get(...)).Then(...)` compile with no
   stray `await`/`.Result` (FR-6 / P7).
7. BenchmarkDotNet shows zero allocations on a 3-step happy-path chain (NFR-2).
8. Core project references **only** the BCL (NFR-1).
9. Packages build, are source-linked, and ship under MIT with attribution (FR/§7).

---

## 10. Open Decisions

| ID | Decision | Resolution |
|----|----------|------------|
| OD-1 | **Name** of the type/package | ✅ **DECIDED — `Result<T>`** in a uniquely-named, org-owned namespace (e.g. `Vostra.Results`). Familiar, zero learning curve, no clash with FluentResults. *Not* "ErrorOr". |
| OD-2 | **Single error vs error list** | ✅ **DECIDED — single, list-capable.** The struct holds **one** error by default; aggregation (FR-9) produces many only when needed (validation accumulation, `Combine`). Leanest; avoids FluentResults' order-dependent mapping. |
| OD-3 | Accept exception-based early-exit (FR-8)? | Recommend **yes, opt-in and documented**; it's the only way to get `?`-feel in C#. *(still open)* |
| OD-4 | Envelope: RFC 7807 `ProblemDetails` vs custom | ✅ **DECIDED — RFC 7807 `ProblemDetails`** for errors (carries `code` + message → enables FR-11.3 typed assertions). Keep a thin success envelope for `operationId` + `data`. |
| OD-5 | Serializer default: System.Text.Json vs Newtonsoft | Recommend **System.Text.Json** (AOT/trim, no dep), Newtonsoft optional in Testing. *(still open)* |
| OD-6 | Retire `AM.Extensions(.AspNetCore)` or wrap it? | Decide at M1 — recommend **retire** once tests pass to avoid two parallel result types. *(still open)* |

---

*Once §10 is settled, scaffold `Core` → `AspNetCore` → `Testing` in that order, using §9 as the
acceptance gate and the integration tests in the reference repo as the live regression suite.*
