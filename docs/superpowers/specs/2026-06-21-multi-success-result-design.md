# Multi-success `Result<T1,T2>` / `Result<T1,T2,T3>` — Design

**Date:** 2026-06-21 · **Status:** Approved (brainstorm), pre-implementation · **Package:** `Vostra.Results` (Core only)

## 1. Problem & intent

Today `Result<T>` is "one value type **or** error(s)". Some operations legitimately succeed as *one of
several distinct value shapes* (e.g. a render that yields either a `Pdf` or an `Html`). Expressing that
today forces either a base-type/`object` success value (loses static typing) or a fake "error" arm for a
non-error outcome (abuses the error channel).

This adds an Option-A discriminated-union success surface — `Result<T1,T2>` and `Result<T1,T2,T3>` — that
holds **one of N typed success values, or the existing `ErrorBase[]` failure**. It is the "richer match
surface / multi-arity union" the requirements explicitly parked as a post-v1 item.

## 2. The single load-bearing decision: terminal match surface, not a monad

These types exist to be **produced** (returned) and **consumed** (matched), never **chained through**.

- **No** `Map`/`Then`/`Tap`/`Ensure`/LINQ/async combinators on the union types.
- Rationale: a combinator over a union is ill-defined ("which arm does `Map` transform?"); OneOf answers
  that with per-arm `MapT0`/`ThenT1`/… which, multiplied by the async × arity matrix (FR-6), is ~60–100
  mechanical methods. Excluding them removes that entire tax and is what makes Option A affordable.
- You chain *into* a union (a method returns one) and `Match` *out* of it. If per-arm chaining is ever
  proven necessary, it is purely additive and can be added later without breaking changes.

## 3. Type shape

Mirrors the existing `Result<T>` struct discipline and OneOf's no-boxing field layout.

```csharp
public readonly partial struct Result<T1, T2> : IEquatable<Result<T1, T2>>
{
    private readonly T1? _value1;
    private readonly T2? _value2;
    private readonly ErrorBase[]? _errors;
    private readonly bool _initialized;
    private readonly int _index;          // 1 or 2 = which success arm; 0 = faulted/uninitialized
}
```

- **One typed field per arm** (not `object`) → value-type arms do **not** box → success path stays
  zero-alloc (NFR-2).
- **Same `ErrorBase[]` failure channel** as `Result<T>` → AspNetCore HTTP mapping and Testing typed-error
  assertions keep working with no change.
- **Same fault discipline:** `IsError => !_initialized || _errors is not null`. `default(Result<T1,T2>)`
  is faulted and surfaces the shared `ResultSentinels.Uninitialized` error (index `0`, `_initialized=false`).
- `Result<T1,T2,T3>` is the same with a third field and `_index ∈ {1,2,3}`.

**Out of scope for the union (v1):** `SuccessKind` (Ok/Created). The union always constructs as a plain
success; the 200/201 distinction belongs to the deferred AspNetCore mapping, so a `SuccessKind` field on
the union would be dead weight. Noted as a follow-up if/when HTTP mapping lands.

## 4. Public surface (v1)

| Member | Notes |
|---|---|
| `implicit operator Result<T1,T2>(T1)` / `(T2)` | Success arm 1 / 2. The DP1 ergonomic win. |
| `implicit operator Result<T1,T2>(ErrorBase)` / `(ErrorBase[])` / `(List<ErrorBase>)` | Failure, identical to `Result<T>`. |
| `Match<TOut>(Func<T1,TOut> onT1, Func<T2,TOut> onT2, Func<IReadOnlyList<ErrorBase>,TOut> onErr)` | Exhaustive incl. error arm. |
| `Switch(Action<T1>, Action<T2>, Action<IReadOnlyList<ErrorBase>>)` | Void counterpart. |
| `bool IsSuccess` / `bool IsError` | Identical semantics to `Result<T>`. |
| `int Index` | `1`/`2`(/`3`) on success; `0` when faulted. Mirrors OneOf's discriminator for callers who want it. |
| `IReadOnlyList<ErrorBase> Errors` / `ErrorBase FirstError` | Identical to `Result<T>`. |
| `Equals`/`==`/`!=`/`GetHashCode`/`ToString` | Value-based (FR-1.4); arm-aware. |

`Result<T1,T2,T3>` adds `onT3`/the third arm throughout.

### Implicit-conversion constraint (documented limitation)

Per-arm implicit operators require `T1`, `T2`(, `T3`) and `ErrorBase` to be **mutually non-convertible**.
If two arms are assignable to one another (e.g. `Result<string, object>`) the C# compiler rejects the
ambiguous implicit operator at the *use* site. This is the same constraint OneOf carries; it is documented,
not worked around. `ErrorBase` subclasses as an arm type are disallowed by this same rule (the error arm
already owns `ErrorBase`).

## 5. Match/Switch semantics

- Success arm `n` invokes `onTn` with that arm's value.
- Faulted (including `default` and explicitly-faulted) invokes `onErr` with `Errors` (never null/empty —
  uninitialized yields the sentinel list, exactly as `Result<T>`).
- `Match`/`Switch` never return `default!` and never expose an invalid value — DP3 preserved.
- A `null` delegate for the active arm throws `ArgumentNullException` (fail fast; OneOf throws
  `InvalidOperationException` but `ArgumentNullException` is the more accurate .NET idiom).

## 6. Equality / hashing / ToString

- Two unions are equal iff same fault-ness, and: if faulted → `Errors.SequenceEqual`; if success → same
  `_index` **and** arm-value equal via `EqualityComparer<Tn>.Default`. (Same structure as `Result<T>`.)
- `GetHashCode` combines `_index` and the active arm's value (null-safe), or aggregates errors when faulted.
- `ToString`: `"Success(<value>)"` for the active arm, `"Error[n](<first>)"` for faults — matching `Result<T>`.

## 7. File layout (follows existing split convention)

- `src/Vostra.Results/ResultOfT1T2.cs` — struct, fields, ctor, construction, implicit ops, equality, ToString.
- `src/Vostra.Results/ResultOfT1T2.Inspection.cs` — `Match`/`Switch`/`Index`/`IsError`/`Errors`.
- `src/Vostra.Results/ResultOfT1T2T3.cs` + `.Inspection.cs` — arity-3 equivalents.

(Two-file-per-type split mirrors `ResultOfT.cs` + `ResultOfT.Inspection.cs`.)

## 8. Tests (TDD, both TFMs net8/net9, FluentAssertions + xUnit)

Per arity, mirroring `ResultOfTTests` style:
- Each arm converts implicitly to the correct success (`Index`, `IsSuccess`, value reachable via `Match`).
- `ErrorBase` / `ErrorBase[]` / `List<ErrorBase>` convert to failure.
- `default(Result<…>)` is faulted with `Result.Uninitialized` code (not a success).
- `Match` routes to the correct arm; `Switch` invokes the correct action; error arm receives full `Errors`.
- Null delegate for the active arm throws `ArgumentNullException`.
- Equality: same-arm equal values equal; different arms / different values not equal; equal failures equal.
- `ToString` distinguishes success vs error.
- (Allocation sanity for the value-type success path is covered by the existing NFR-2 approach; not a new bench in this branch.)

## 9. Explicitly deferred (not this branch)

- **Per-arm chaining / async / LINQ** on unions (§2).
- **Arity ≥ 4** and any **source generator** (hand-rolled 2+3 only; preserves the zero-codegen identity).
- **AspNetCore** `ToHttpResponse` over a union (open question: which arm → which status) and **Testing**
  assertions for unions. The error channel already flows through unchanged, so unions are usable in an API
  today via `Match`; the success-arm-to-HTTP mapping gets its own brainstorm + branch.

## 10. Acceptance

- A method can `return pdf;` or `return html;` or `return new NotFoundError(...);` for a
  `Result<Pdf,Html>` with no factory ceremony (DP1).
- No invalid value is ever observable; all consumption is `Match`/`Switch` (DP3).
- Core still references only the BCL; no new dependency, no generator (NFR-1, DP5).
- Full suite green on net8.0 and net9.0.
