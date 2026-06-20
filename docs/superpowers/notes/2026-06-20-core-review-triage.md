# Core review triage (from independent reviewer, 2026-06-20)

Decisions: **FIX** = do it in the Core v1.1 pass before AspNetCore. **DEFER** = later cycle, recorded.
**DROP** = won't do, recorded. (TBD = awaiting user call.)

## FIX — Core v1.1 (before AspNetCore)
| # | Item | Action |
|---|------|--------|
| 1 | **Created distinction (OD-7)** — adopt B | Add neutral `SuccessKind { Ok, Created }` to `Result<T>` + non-generic `Result`; `Result.Created(value)` factory; implicit/`Ok` default to `Ok`. Success path stays zero-alloc. |
| 2 | **Non-generic `Result` under-built** | Add parity surface: `Match`/`Switch`/`Tap`/`TapError`/`MapError`/`Then` (void→`Result`/`Result<T>`). Removes the `if (IsError)` footgun on void/`Delete`-style ops. |
| 3 | **Spec↔code drift: Error.Prefix** | Spec's flagship example uses `Error.Prefix("shipping")` which doesn't exist. Add small re-wrap helpers on `Error` (`WithCode`/`Prefix`/`WithMetadata`) that PRESERVE type+code+CausedBy (also satisfies FR-2.4/P11), and make the spec compile. |
| 4 | **Spec↔code drift: Error.Equals** | Code ignores `CausedBy`/metadata in equality (good for FR-11.3 identity asserts). Fix the SPEC to match the code (keep code). |
| 5 | **FR-9.1 collector** | Add `Combine`/`Sequence` over `IEnumerable<Result<T>>` (params variant already exists). |
| 6 | **SelectAsync contract** | Doc/comment: `selector` should return errors-as-values, not throw (keeps deterministic completion). |

## TBD — user to decide fix-now vs defer
| # | Item | Question |
|---|------|----------|
| 7 | **ErrorType.Failure dangling** | `Failure` enum member has no producing subclass. Add `FailureError`, or drop `Failure` and use `Unexpected`? |
| 8 | **Serialization (FR-12, STJ converter)** | `Result<T>` has no public `Value` → won't round-trip under System.Text.Json without a custom `JsonConverter`. Testing (FR-11) depends on it. Build in v1.1, or defer to its own cycle? |
| 9 | **NFR gates** | Add BenchmarkDotNet zero-alloc gate (NFR-2/§9.7) + coverage run (NFR-7 ≥90%) now, or defer? Block README claims until measured either way. |

## DROP / DEFER (recorded)
| # | Item | Decision |
|---|------|----------|
| 10 | OD-3 early-exit `Scope`/`OrReturn` | **DEFER** (revisit after Testing). Purely additive — adding it later breaks nothing. LINQ `from…from…` + `Then` already cover P5; revisit only if porting the reference repo's complex service methods proves awkward. If built: opt-in, documented (no hot loops), possibly a separate add-on. |
| 11 | OD-5 serializer default | **DECIDED: System.Text.Json** (Newtonsoft optional in Testing). |
| 12 | OD-6 retire old AM.Extensions | **DEFER** — retire, but only after AspNetCore+Testing exist and reference integration tests pass (last migration gate). |
| 13 | ValueTask async matrix (FR-6.1) | **DEFER** — Task-only matrix is enough; ValueTask is a perf refinement. |
