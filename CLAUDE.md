# CLAUDE.md — `vostra-result`

A lean, async-friendly, dependency-free `Result<T>` type for .NET — plus ASP.NET Core mapping and an
integration-testing toolkit. Released under MIT.

## How we work
Commit message must never contain : `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>` or anything related, constantly check and clenan out any Claude-related information in commits and code


Design-first: before scaffolding or writing non-trivial code, run a **brainstorming** session
(superpowers `brainstorming` skill) to think through the design, surface trade-offs, and settle open
questions. Code follows an agreed design, not the other way around.

## Status

**All three packages are built, tested, and merged to `main` (and pushed).** Code-complete, **not yet
published to NuGet** — publishing + pre-1.0 hardening is the next phase.

- **Core** (`Vostra.Results`) — `Result`, `Result<T>`, plus multi-success unions `Result<T1,T2>` /
  `Result<T1,T2,T3>` (added 2026-06-21); `ErrorBase` + built-in kinds; Match/Switch/TryGet; sync + async
  combinator matrix; LINQ; aggregation. Zero runtime deps.
- **AspNetCore** (`Vostra.Results.AspNetCore`) — `ToHttpResponse`, RFC 7807 envelopes, DI status map.
- **Testing** (`Vostra.Results.Testing`) — `TestHttpClient` → `Result<T>`, typed-error reconstruction,
  zero-dep fluent assertions.

~194 tests/TFM, green on **net8.0 + net9.0**, 0 warnings.

## Start here

- **[cc/plans/2026-06-22-nuget-and-prerelease-hardening.md](cc/plans/2026-06-22-nuget-and-prerelease-hardening.md)**
  — the active worklist: NuGet publishing, packaging gaps, and the full-project review findings.
- **[docs/usage.md](docs/usage.md)** — the user-facing usage guide (all three packages).
- **[cc/docs/requirements/result-type-requirements.md](cc/docs/requirements/result-type-requirements.md)**
  — the original spec. Functional requirements (`FR-*`), non-functional (`NFR-*`), pain points (`P*`),
  strengths preserved (`S*`), acceptance criteria (§9), open decisions (§10).
- **[cc/docs/requirements/result-pattern.md](cc/docs/requirements/result-pattern.md)**
  — walkthrough of the **existing** FluentResults-based implementation this library replaces.
- Design specs for each package live under `docs/superpowers/specs/`.

## Reference repository

The requirements were derived from an existing implementation. Both docs link into it by `file:line`:

```
C:\Users\Robert\source\repos\_ARCHIVE\VCC\popcat-assortment-admin-api
```

That repo is the **live regression suite** for migration (see requirements §8) — its integration
tests should pass once this library is mature and swapped in.

## Borrow sources (local clones)

Full source of the libraries we borrow from (requirements §7) is checked out locally — read them
directly instead of guessing at their APIs:

- **ErrorOr** — `C:\Users\Robert\source\repos\EXTERNA\error-or` (`src/`, `tests/`, `LICENSE.md`).
  Reference for: implicit `T`/`Error` conversions, `Match`/`Switch`, `Then`/`ThenAsync`, `ErrorType`+code.
- **FluentResults** — `C:\Users\Robert\source\repos\EXTERNA\FluentResults` (`src/`, `LICENSE`).
  Reference for: typed `IError`, `CausedBy(exception)`, metadata, `Merge`.
- **OneOf** — `C:\Users\Robert\source\repos\EXTERNA\OneOf` (`OneOf/`, `licence.md`; MIT, Harry McIntyre).
  Reference for: discriminated-union / exhaustive `Match`/`Switch` ergonomics and the no-box field-per-arm
  layout. **Used for the multi-success unions** `Result<T1,T2>` / `Result<T1,T2,T3>` (done 2026-06-21,
  hand-rolled arities 2–3, no source generator). Spec: `docs/superpowers/specs/2026-06-21-multi-success-result-design.md`.
- **FluentAssertions** — `C:\Users\Robert\source\repos\EXTERNA\fluentassertions` (`Src/`, `Tests/`, `LICENSE`).
  Reference **for the Testing package** — read its assertion API shape and diagnostic-message ergonomics
  (failure phrasing, `Should().Be...` chaining). The local checkout is **v8 (8.10) — commercially licensed**.
  ⚠️ **Inspiration ONLY — never copy code from it.** Our assertion helpers are hand-rolled zero-dep (see the
  Testing design); we do not take a runtime dependency on FluentAssertions.

ErrorOr, FluentResults, and OneOf are **MIT** — code may be lifted/adapted **with attribution** (retain the
upstream license notice; credit in the README / THIRD-PARTY-NOTICES, requirements §7). **FluentAssertions
(v8/8.10) is the exception: commercially licensed — reference for inspiration only, never copy.**

## Decided (requirements §10)

- Type/namespace: **`Result<T>`** in `Vostra.Results` (not "ErrorOr").
- Error model: **single error, list-capable** (aggregate only when needed).
- HTTP error envelope: **RFC 7807 `ProblemDetails`**.
- Serializer default (OD-5): **System.Text.Json** (`RawJsonFormat`); injectable via `IResultRawFormat`.
- Multi-success unions are a **terminal match surface** by design (no Map/Then/LINQ through a union); the
  library does **not** null-check combinator delegates (uniform convention — a null delegate NREs).

Still open / deferred: early-exit `Scope`/`OrReturn` via control-flow exception (OD-3); retire-vs-wrap the
old `AM.Extensions` layer in the reference repo (OD-6); `ValueTask` matrix; BenchmarkDotNet zero-alloc proof
(NFR-2 §9.7 — needed before 1.0).

## Working in the codebase

- **Build/test:** `dotnet test` (multi-targets net8.0 + net9.0; TFMs are per-`.csproj`, **not** in
  `Directory.Build.props`). 0-warning bar.
- **Push:** via **WSL `gh`** (authenticated as `rcn123`), **not** Windows Git Bash. Remote `origin` =
  `rcn-vostra/vostra-result`. Pushing to `main` may require explicit user authorization per commit.
- **Quirk:** the IDE intermittently strips project entries from `Vostra.Results.sln` (uncommitted); if
  `dotnet test` says "Unable to find a project to restore", run `git restore Vostra.Results.sln`.
- **FluentAssertions 8.x** (test-only) is commercially licensed — flagged for resolution before publishing.


# Clean out any Claude-related infrormation
Commit message must never contain : `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>` or anything related, constantly check and clenan out any Claude-related information in commits and code
