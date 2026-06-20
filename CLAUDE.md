# CLAUDE.md — `vostra-result`

A lean, async-friendly, dependency-free `Result<T>` type for .NET — plus ASP.NET Core mapping and an
integration-testing toolkit. Released under MIT.

## How we work
Commit message must never contain : `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>` or anything related, constantly check and clenan out any Claude-related information in commits and code


Design-first: before scaffolding or writing non-trivial code, run a **brainstorming** session
(superpowers `brainstorming` skill) to think through the design, surface trade-offs, and settle open
questions. Code follows an agreed design, not the other way around.

## Status

Greenfield. **No code yet** — start from the requirements.

## Start here

- **[cc/docs/requirements/result-type-requirements.md](cc/docs/requirements/result-type-requirements.md)**
  — the spec. Functional requirements (`FR-*`), non-functional (`NFR-*`), pain points being fixed
  (`P*`), strengths being preserved (`S*`), acceptance criteria (§9), and open decisions (§10).
- **[cc/docs/requirements/result-pattern.md](cc/docs/requirements/result-pattern.md)**
  — walkthrough of the **existing** FluentResults-based implementation this library replaces.

## Reference repository

The requirements were derived from an existing implementation. Both docs link into it by `file:line`:

```
C:\Users\Robert\source\repos\_ARCHIVE\VCC\popcat-assortment-admin-api
```

That repo is the **live regression suite** for migration (see requirements §8) — its integration
tests should pass once this library is mature and swapped in.

## Borrow sources (local clones, MIT)

Full source of the libraries we borrow from (requirements §7) is checked out locally — read them
directly instead of guessing at their APIs:

- **ErrorOr** — `C:\Users\Robert\source\repos\EXTERNA\error-or` (`src/`, `tests/`, `LICENSE.md`).
  Reference for: implicit `T`/`Error` conversions, `Match`/`Switch`, `Then`/`ThenAsync`, `ErrorType`+code.
- **FluentResults** — `C:\Users\Robert\source\repos\EXTERNA\FluentResults` (`src/`, `LICENSE`).
  Reference for: typed `IError`, `CausedBy(exception)`, metadata, `Merge`.
- **OneOf** — `C:\Users\Robert\source\repos\EXTERNA\OneOf` (`OneOf/`, `licence.md`; MIT, Harry McIntyre).
  Reference for: discriminated-union / exhaustive `Match`/`Switch` ergonomics and the source-generator
  approach. **Not needed for Core v1** — keep in mind for later (richer match surface, multi-arity unions).

All are **MIT** — code may be lifted with attribution; retain license notices and credit them in the
README / THIRD-PARTY-NOTICES (requirements §7).

## Decided so far (requirements §10)

- Type/namespace: **`Result<T>`** in `Vostra.Results` (not "ErrorOr").
- Error model: **single error, list-capable** (aggregate only when needed).
- HTTP error envelope: **RFC 7807 `ProblemDetails`**.

Still open: early-exit via control-flow exception (OD-3), serializer default (OD-5), retire-vs-wrap the
old layer (OD-6).

## Build order

Scaffold in this sequence, using requirements §9 as the gate: **`Core` → `AspNetCore` → `Testing`**.
Highest-leverage first step is the `Core` `Result<T>` struct + `Error` with implicit conversions and
`Match` (FR-1, FR-3, FR-4) — that alone resolves P1/P3 and the `default!` footgun.


# Clean out any Claude-related infrormation
Commit message must never contain : `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>` or anything related, constantly check and clenan out any Claude-related information in commits and code
