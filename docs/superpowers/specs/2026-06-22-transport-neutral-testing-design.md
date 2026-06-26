# Design — Liberate the chain-and-assert layer (transport-neutral Testing)

**Date:** 2026-06-22 · **Status:** Approved (brainstorm) · **Scope:** packaging split of `Vostra.Result.Testing`.
**Origin:** Maximo adapter team feedback, ask #1 ([cc/docs-in/request-for-changes-wom-masa.md](../../../cc/docs-in/request-for-changes-wom-masa.md)).

> *"The verbs are the replaceable part; the composition is the treasure. Don't bury the treasure inside
> the HTTP package."* — make the chain-and-assert layer usable over **any** `Task<Result<T>>`, with no
> ASP.NET Core dependency.

---

## 1. Problem

The `Vostra.Result.Testing` package mixes two concerns in one assembly that references
`Vostra.Result.AspNetCore`:

1. **The treasure (transport-neutral):** the fluent chain-and-assert layer —
   `Then` (Core), `ShouldBeSuccess`/`ShouldHaveError`/`Assert`/`ShouldBeNotFound`/… over
   `Result<T>` and `Task<Result<T>>`, with rich failure diagnostics.
2. **The HTTP adapter:** `TestHttpClient` (Get/Post/…), RFC 7807 reconstruction, response serialization,
   pagination.

A non-HTTP consumer (e.g. a queue/ACK-NACK service) cannot use (1) without dragging in (2) and the entire
ASP.NET Core framework reference. Verified: the assertion files already compile against Core only — the
AspNetCore dependency comes solely from the HTTP-adapter files.

## 2. Decision — split into two packages

```
Vostra.Result                                  (Core, zero-dep)
   ├── Vostra.Result.AspNetCore                 (production Result -> HTTP)
   │      └── Vostra.Result.AspNetCore.Testing  (HTTP test client)   ← NEW package
   └── Vostra.Result.Testing                    (chain + assert)     ← now Core-only
```

- **`Vostra.Result.Testing`** depends **only** on `Vostra.Result`. Transport-neutral; this is what a
  non-HTTP consumer references.
- **`Vostra.Result.AspNetCore.Testing`** depends on `Vostra.Result.AspNetCore` **and**
  `Vostra.Result.Testing`. Naming follows the framework convention (`Microsoft.AspNetCore.Mvc.Testing`)
  and mirrors the production `Vostra.Result.AspNetCore` package.

This is a pre-1.0, pre-NuGet reorg: no published consumers, behavior of every moved type unchanged.

### 2.1 File moves (verified by actual `using`/type coupling)

**`Vostra.Result.Testing` (neutral, Core-only):**
- `ResultAssertions.cs`, `ResultTaskAssertions.cs`, `VostraAssertionException.cs`
- `RequestContext.cs` (see §3 — renamed fields, now neutral)
- **NEW** `RequestContextExtensions.cs` — the public metadata-key const + `WithRequestContext` helper (§3)

**`Vostra.Result.AspNetCore.Testing` (HTTP adapter):**
- `TestHttpClient.cs`, `ProblemDetailsErrorReader.cs`, `RawJsonFormat.cs`, `IResultRawFormat.cs`,
  `PagedList.cs`

### 2.2 Namespaces

- Neutral types stay in `namespace Vostra.Result.Testing`.
- HTTP types move to `namespace Vostra.Result.AspNetCore.Testing` (namespace matches package). Existing
  internal test `using`s update by one line.

## 3. Diagnostics — `RequestContext` becomes neutral and request-only

`RequestContext` is a **diagnostic breadcrumb only** — it carries no behavior; it is rendered solely in
assertion-failure messages to say *what was attempted*. It is attached to the failing error's metadata and
read back by `ResultAssertions.Describe`.

### 3.1 Neutralize the fields

```csharp
namespace Vostra.Result.Testing;

/// <summary>Describes the operation that produced a failed result — attached to the failing error so
/// assertion failure messages can show what was attempted. Diagnostic only; carries no behavior.</summary>
/// <param name="Operation">What was attempted — an HTTP verb ("GET"), a queue action ("SEND"), etc.</param>
/// <param name="Target">Where — a request URL, a queue/topic name, an endpoint.</param>
/// <param name="Body">The payload sent, if any.</param>
public sealed record RequestContext(string Operation, string Target, object? Body);
```

Rationale (settled in brainstorm): the **only** callers who hand-construct this type are non-HTTP
consumers — `TestHttpClient` builds it for HTTP users, who merely read the rendered line. So the
constructor's vocabulary is optimized for the non-HTTP hand-author, and the neutral package carries **zero**
HTTP vocabulary. Single neutral type (no `HttpRequestContext`): the HTTP adapter translates verb→`Operation`,
url→`Target` at construction.

**Request-only, by design.** The breadcrumb captures the *attempt* (input). The *outcome* (including the
error `Code`, `Type`, `Message`, optional `CausedBy`) is fully carried by the typed `Error` the context is
attached to — so there is no error code in `RequestContext` (it would duplicate state that can drift). We do
**not** model a request+response "exchange" context, and we do **not** preserve the raw HTTP status as a
first-class field: the inbound reader already collapses any status into one of the six `ErrorType`s, which
is the intended abstraction. (If a test ever needs the literal status, the HTTP adapter may drop it into
metadata as a plain value, rendered generically — not a promised feature.)

### 3.2 Sanctioned attach path (public, in the neutral package)

Today `metadata["request"]` is a private contract between `TestHttpClient` and `Describe`. Make it public so
any transport can participate:

```csharp
namespace Vostra.Result.Testing;

public static class RequestContextExtensions
{
    /// <summary>Metadata key under which a <see cref="RequestContext"/> is attached to an error.</summary>
    public const string RequestMetadataKey = "request";

    /// <summary>Returns a copy of <paramref name="error"/> with <paramref name="context"/> attached for
    /// diagnostics, preserving any existing metadata and the concrete error type.</summary>
    public static ErrorBase WithRequestContext(this ErrorBase error, RequestContext context)
    {
        var metadata = error.Metadata is null
            ? new Dictionary<string, object?>()
            : new Dictionary<string, object?>(error.Metadata);
        metadata[RequestMetadataKey] = context;
        return error.WithMetadata(metadata);
    }
}
```

- `ResultAssertions.Describe` reads `error.Metadata[RequestMetadataKey]`, and when the value is a
  `RequestContext` renders `request: {Operation} {Target}` (+ ` | body: {Body}` when present).
- The HTTP adapter's `ProblemDetailsErrorReader`/`TestHttpClient` attach via this **same** helper
  (translating HTTP verb/url into `Operation`/`Target`) — one path for all transports.

## 4. Test projects

Split `tests/Vostra.Result.Testing.Tests` into two:

- **`Vostra.Result.Testing.Tests`** — assertion + chaining + `WithRequestContext`/`Describe` tests.
  References **only** the neutral package. This *structurally proves* the neutral package has no AspNetCore
  leak: it would not compile if a moved type still pulled in HTTP.
- **`Vostra.Result.AspNetCore.Testing.Tests`** — `TestHttpClient` round-trip / reconstruction / pagination
  tests. References the HTTP-adapter package.

## 5. Out of scope — what we are NOT building (ask #1 = "A: liberate + document")

- **No messaging/Service Bus adapter.** The consumer owns their transport; they supply any function
  returning `Task<Result<T>>` and use the neutral chain+assert over it. (§4 Non-Goals: not solving
  distributed/transactional concerns.)
- **No change to `ErrorType` / the error model.** The six-`ErrorType` taxonomy + per-error `Code` stays;
  it is the transport-neutral category that powers the open/closed status mapping and category assertions.

## 6. Documentation

- **`docs/usage.md` §3** reworked: lead with the transport-neutral chain+assert over *any*
  `Task<Result<T>>` (a non-HTTP example using `WithRequestContext`), then present `TestHttpClient` as one
  adapter package on top.
- Split / retarget the package `README.md`s (neutral vs HTTP-adapter).
- Update **CLAUDE.md** package list and the **hardening plan** publish-order note: publish order becomes
  Core → {AspNetCore, Testing}; AspNetCore → AspNetCore.Testing (Testing no longer depends on AspNetCore).

## 7. Acceptance

- A test project referencing **only** `Vostra.Result.Testing` compiles and runs the full chain+assert
  surface (proves no AspNetCore dependency).
- HTTP tests pass unchanged behaviorally through `Vostra.Result.AspNetCore.Testing`.
- Assertion-failure diagnostics render the renamed `Operation`/`Target` for both HTTP and a non-HTTP
  attach, via the shared `WithRequestContext` path.
- `dotnet test` green on net8.0 + net9.0, 0 warnings.
