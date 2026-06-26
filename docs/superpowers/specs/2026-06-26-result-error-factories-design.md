# Discoverable error factories on `Result` — design

**Date:** 2026-06-26
**Status:** Approved (brainstorming) → implementing
**Package:** `Vostra.Results` (Core)

## Problem

To return a failure today you write `new NotFoundError(...)` and rely on the implicit
`ErrorBase → Result<T>` conversion. That works, but it is **not discoverable**: a developer at the call
site has no way to ask "what failure kinds exist?" without knowing the concrete type names up front.

We want the IDE to answer that question. Type `Result.` and IntelliSense should list every built-in
failure kind — mirroring how `Result.Success`, `Result.Created()`, `Result.Ok<T>()` and `Result.Fail<T>()`
already make the *success* side discoverable from the single `Result` entry point.

## Decision

Add one **static factory per built-in error kind** to the non-generic `Result` struct (which already
documents itself as *"the single discoverable entry point for explicit factories"*), in a new partial file
`src/Vostra.Results/Result.Errors.cs`.

| Factory | Returns | `ErrorType` | Default `code` |
|---|---|---|---|
| `Result.ValidationError(...)` | `ValidationError` | `Validation` | `General.Validation` |
| `Result.NotFoundError(...)` | `NotFoundError` | `NotFound` | `General.NotFound` |
| `Result.ConflictError(...)` | `ConflictError` | `Conflict` | `General.Conflict` |
| `Result.AlreadyExistsError(...)` | `AlreadyExistsError` | `Conflict` | `General.AlreadyExists` |
| `Result.UnauthorizedError(...)` | `UnauthorizedError` | `Unauthorized` | `General.Unauthorized` |
| `Result.ForbiddenError(...)` | `ForbiddenError` | `Forbidden` | `General.Forbidden` |
| `Result.Error(...)` | `Error` (catch-all) | `Unexpected` | `General.Unexpected` |

### Shape

Each factory is **pure pass-through sugar** over the existing public constructor and mirrors it exactly —
`message` first (required), `code` optional (the kind fallback), then `causedBy` and `metadata`:

```csharp
public static NotFoundError NotFoundError(
    string message,
    string code = "General.NotFound",
    Exception? causedBy = null,
    IReadOnlyDictionary<string, object?>? metadata = null) =>
    new(message, code, causedBy, metadata);
```

- **Returns the concrete error type** (not `ErrorBase`) so the existing implicit operators carry it to
  `Result` / `Result<T>` with no new conversions, and a caller who wants to keep building
  (`.WithMetadata(...)`) still can.
- **Naming mirrors the class names** (the `...Error` suffix) — chosen over short `NotFound`/`Validation`
  names so the factory list reads identically to the types you'd otherwise `new`.
- **Message-first** keeps the factory consistent with the constructor it wraps. Callers who prefer
  ErrorOr's code-leading style use named arguments: `Result.NotFoundError(code: "Order.NotFound",
  message: "...")`.
- The method name `Result.Error(...)` intentionally equals the `Error` type name; no non-generic `Result`
  struct member references the bare `Error` type, so there is no shadowing conflict (verified).

### Guidance: when a `code` is worth supplying

A `code` earns its keep **only when it carries information the kind + call-site context don't already
give.** This rule drives the documentation examples:

- **Worth it** — `ValidationError("Email address is invalid", "EmailAddress.Invalid")`: the code names the
  *field/rule*, which `ErrorType.Validation` cannot. `ConflictError("...", "Order.AlreadyCancelled")`: the
  code names *which* conflict.
- **Worth it** — when one operation can emit the same kind for *different* entities (e.g. `PlaceOrder`
  returning `Customer.NotFound` vs `Product.NotFound`), the code is the only thing that distinguishes them.
- **Not worth it** — `NotFoundError("Order 5 not found", "Order.NotFound")` on a single-entity getter
  returning `Result<Order>`: the entity is fixed by the return type and the kind by the factory, so the
  code restates what is already known. Skip it; let the kind speak and put the entity in the message.

Docs will therefore show NotFound examples **without** a code, and Validation/Conflict examples **with**
one, rather than teaching the redundant `Entity.NotFound` pattern.

## Rejected alternatives

- **Short kind names** (`Result.NotFound(...)`): cleaner, but the user chose full `...Error` names to match
  the concrete class names.
- **Drop `code`, use `.WithCode(...)`**: makes the common "supply a code" case a second call; rejected for
  ergonomics.
- **Generic entity overload** (`Result.NotFoundError<Order>()` deriving `"Order.NotFound"` from
  `typeof(T).Name`): rejected. Real-world codes carry a *specific reason* (`Order.InvalidStatus`,
  `Order.AlreadyCancelled`, `Inventory.InsufficientStock`, `Auth.MissingToken`) that no type name can
  produce — the generic shortcut only ever yields coarse `Entity.Kind` codes and would teach the wrong
  pattern. It also couples the wire/test contract to internal type names (rename → silent code change).

## Scope guard (YAGNI)

No new error kinds, no generic overloads, no changes to `ErrorBase` / `ErrorType` / aggregation. The
existing `new XxxError(...)` form keeps working unchanged — this is additive sugar only.

## Tests (usage-first)

A new `tests/Vostra.Results.Tests/ResultErrorFactoriesTests.cs`:

- Each factory produces an error **equal to its hand-`new`'d twin** (covers concrete type, `Type`, `Code`,
  `Message`).
- Default `code` is the kind fallback; an explicit `code` is honored.
- `causedBy` / `metadata` flow through.
- A factory result **implicitly converts to a failed `Result`** and to a failed `Result<T>` (the call-site
  payoff — demonstrated in a `Result<Order>`-returning method via the ternary).

## Docs to update

- `README.md` — the headline example → `Result.NotFoundError(...)`.
- `docs/usage.md` — the example(s) and the "happy path is implicit" note; add a short note on the factory
  surface and the *when-to-supply-a-code* guidance.
- `src/Vostra.Results/README.md` — mention the discoverable factory surface.
