# Design — `Vostra.Results.Testing` (integration-testing toolkit)

**Status:** Approved for planning · **Date:** 2026-06-21 · **Build order:** Core → AspNetCore → **Testing** (this).

Implements **FR-11** (and consumes FR-12) of
[result-type-requirements.md](../../../cc/docs/requirements/result-type-requirements.md). Preserves
strengths **S5–S8** and fixes pains **P2, P12, P13**. This is the third and final package of the v1 family.

> **Terminology.** *Raw JSON* / *raw format* = the serialized JSON payload that crosses the HTTP connection
> between the server and the test client (earlier drafts called this "the wire"), as opposed to the in-memory
> `Result<T>` / `ErrorBase` objects on either end. The package's job is to translate between the two.

---

## 1. Goal

Collapse an HTTP round-trip back into a `Result<T>` so integration tests read as domain scripts, and make
the **typed error identity survive the round-trip** — a test asserts `ShouldHaveError("Order.NotFound")` or
`ShouldHaveError(ErrorType.NotFound)` instead of substring-matching messages (fixes **P2**). Fully async (no
`.Result`, fixes **P12**) and module-agnostic (fixes **P13**). The package is the *consumer/inverse* of the
`Vostra.Results.AspNetCore` HTTP response contract.

### Non-goals (this slice)
- **No** `WebApplicationFactory` convenience adapter — deferred to the **next slice** (the core client takes an
  `HttpClient`; callers wire their own host). Tracked as future work in §11.
- **No** Newtonsoft implementation — only the injection seam (FR-11.5 requires the seam, not a second impl).
- Not a general HTTP client; only the verbs and envelope shapes the AspNetCore package emits.

---

## 2. Package shape & dependencies

- New project `src/Vostra.Results.Testing/Vostra.Results.Testing.csproj`, TFMs **`net8.0;net9.0`** (parity with
  Core/AspNetCore; a repo-wide `net10.0` bump is a separate decision — the .NET 10 SDK is now installed).
- **Runtime dependency:** `Vostra.Results.AspNetCore` (which transitively brings Core **and** the
  `Microsoft.AspNetCore.App` shared framework). `System.Text.Json` is in-box. **No** assertion library,
  **no** Newtonsoft.
- **This intentionally amends NFR-1** ("Testing: only the serializer + assertions lib"). Rationale: this
  package exists *only* to test an API built with `Vostra.Results.AspNetCore`; it parses that package's exact
  response contract (envelopes + `problem+json`). So it is already 100% coupled to that contract — the only
  question is whether the coupling is *honest* (reference the package, reuse the real types) or *hidden* (copy
  the DTOs and risk drift). Referencing directly reuses the real `SuccessEnvelope<T>`/`ListEnvelope<T>`/
  `Pagination` → **zero duplication, zero drift, one `Pagination` type** (no naming conflict). Every realistic
  consumer already has the ASP.NET Core framework (their system-under-test uses it; in-process hosting needs
  `Mvc.Testing`), and that shared framework ships with the .NET SDK — so there is **no added runtime burden**
  for test hosts. NFR-1's leanness clause was guarding a framework-free consumer that does not exist for an
  ASP.NET-specific test client. Note: **P13 "module-agnostic" means reusable across the app's *feature
  modules*, not host-agnostic** — it does not require avoiding ASP.NET Core.
- `Microsoft.AspNetCore.Mvc.Testing` (`WebApplicationFactory`) stays **test-only** in this slice (used by the
  round-trip tests, §9); the WAF convenience helper is the next slice (§11).
- No `InternalsVisibleTo`; the package uses the public surface of Core and AspNetCore only.

> **Assertion-library note:** FluentAssertions v8/8.10 is commercially licensed — used for *inspiration only*,
> never copied, never referenced. Our assertions are hand-rolled zero-dep (§7).

---

## 3. The HTTP response contract we consume (from `Vostra.Results.AspNetCore`)

Recorded here so reconstruction never drifts from the producer.

**Success — scalar** (`SuccessEnvelope<T>`), 200 or 201:
```json
{ "operationId": "…", "data": { … } }
```
**Success — valueless** (`SuccessNoDataEnvelope`), non-generic result — no `data` field:
```json
{ "operationId": "…" }
```
**Success — list** (`ListEnvelope<T>`):
```json
{ "operationId": "…", "data": [ … ], "pagination": { "page":1, "pageSize":20, "totalCount":57, "totalPages":3 } }
```
**Error — single / general** (`ProblemDetails`, `application/problem+json`):
```json
{ "type":"about:blank", "title":"Not Found", "status":404, "detail":"Order 7 not found",
  "code":"Order.NotFound", "errorType":"NotFound", "operationId":"…" }
```
**Error — multi (non-validation)** — adds an `errors` **array**:
```json
{ "…":"…", "code":"…", "errorType":"Conflict",
  "errors":[ {"code":"A.X","message":"…"}, {"code":"A.Y","message":"…"} ] }
```
**Error — validation** (`HttpValidationProblemDetails`) — `errors` is a field→messages **object map**:
```json
{ "type":"about:blank", "title":"Bad Request", "status":400,
  "errors": { "name":["Required."], "age":["Must be ≥ 0."] },
  "code":"General.Validation", "errorType":"Validation", "operationId":"…" }
```

**Critical hazard:** the JSON key `errors` has **two incompatible shapes** — an object map (validation) vs an
array of `{code,message}` (multi non-validation) — and is **absent** for single errors. Reconstruction MUST
branch on `errorType == "Validation"` before touching `errors` (§5). Identity for errors lives in the
`code`/`errorType`/`detail` extensions, **not** the RFC `type` URI (always `about:blank`).

---

## 4. Architecture (units & responsibilities)

```
TestHttpClient ──uses──▶ IResultRawFormat ──uses──▶ ProblemDetailsErrorReader
   (verbs, request ctx)     (serialize/deserialize)     (errorType → typed ErrorBase)
        │
        └── returns Result<T> / Result / Result<PagedList<T>>  ──asserted by──▶ Result(Task)Assertions
                                                                                 (throws VostraAssertionException)
```

Four small units, each independently testable:

| Unit | Responsibility | Knows about |
|------|----------------|-------------|
| `TestHttpClient` | issue HTTP verbs, own **request context** (verb, URL, body), map response→`Result` | `HttpClient`, `IResultRawFormat` |
| `IResultRawFormat` (+ `RawJsonFormat`) | serialize requests; deserialize success payloads (strict on `T`); reconstruct raw errors | `System.Text.Json`, the **reused** AspNetCore envelopes, `ProblemDetailsErrorReader` |
| `ProblemDetailsErrorReader` | map `errorType`/`code`/`detail` (+ field map / error array) → typed `ErrorBase`(s) | Core error kinds |
| `Result(Task)Assertions` | fluent, chainable assertions; compose the rich S6 message at throw-time | `Result<T>`, request context metadata |

### 4.1 The S6 layering fix (load-bearing)
The rich failure message (verb, URL, request body, server error — **S6**) needs data the *raw-format reader
never sees* (it only gets the `HttpResponseMessage`). Resolution:
1. `IResultRawFormat.ReadErrors` returns the **raw** reconstructed `ErrorBase` list (server identity only —
   `code`, `errorType`, `detail`, and `status` in metadata).
2. `TestHttpClient` attaches request context to the failing result's first error as
   `metadata["request"] = { verb, url, requestBody }` — **without** overwriting `Message`, so typed identity
   (FR-11.3) and the server's own message both survive.
3. `VostraAssertionException` composes the full S6 diagnostic blob **at throw-time** from the error + that
   metadata. The expensive message only materializes when an assertion actually fails.

---

## 5. `IResultRawFormat` seam (FR-11.5) + reconstruction

```csharp
public interface IResultRawFormat
{
    HttpContent SerializeRequest(object body);
    Task<T?> ReadData<T>(HttpResponseMessage response, CancellationToken ct);
    Task<IReadOnlyList<ErrorBase>> ReadErrors(HttpResponseMessage response, CancellationToken ct);
}
```
Single seam carries **both** "which serializer" and "which envelope shape" (FR-11.5). Default
`RawJsonFormat` uses `System.Text.Json`.

**`ReadData<T>`** — deserializes the **reused** `Vostra.Results.AspNetCore.SuccessEnvelope<T>.data` (scalar) /
`ListEnvelope<T>` (list) — the exact types the server emits, so the round-trip cannot drift. The valueless
path (`SuccessNoDataEnvelope`) is handled by the non-generic client method and does **not** read `data`.

**S7 strict-deserialization guard** — `UnmappedMemberHandling.Disallow` scoped to the **payload `T`** (not the
envelope, which must stay tolerant to future fields). On the STJ "member not found" failure, rethrow with the
preserved reference hint: *"…MAKE SURE YOU HAVE MAPPED TO THE CORRECT RESPONSE OBJECT (THAT CONTROLLER RESPONDS
WITH)"*.

**`ReadErrors` / `ProblemDetailsErrorReader`** — branch precisely:
1. Parse the problem document. Read `errorType` (string, enum-as-string, case-tolerant), `code`, `detail`,
   `status`.
2. **If `errorType == "Validation"`** → read `errors` as an **object map**; emit one `ValidationError` per
   field message with `code` = top-level `code` (per-field codes are not in the response — documented
   limitation) and `metadata["field"]` = the map key.
3. **Else if `errors` present as an array** → emit one typed error per `{code,message}` entry (type from the
   top-level `errorType`).
4. **Else (single error)** → one typed error from `code`/`errorType`/`detail`.
5. **Unknown `errorType` / missing `code` / non-JSON / empty body** → a single `Error` (Unexpected) whose
   `Message` is the raw body (or a status-only message when empty); HTTP `status` preserved in metadata. This
   subsumes the reference helper's plain-text and framework-`ProblemDetails` fallbacks.

`errorType` → concrete kind map: `Validation`→`ValidationError`, `NotFound`→`NotFoundError`,
`Conflict`→`ConflictError`, `Unauthorized`→`UnauthorizedError`, `Forbidden`→`ForbiddenError`,
`Unexpected`→`Error`. (`Conflict` always rebuilds as `ConflictError` even if the server raised
`AlreadyExistsError`; both share `code`+`Type` and assertions key on `code`, so identity is preserved.)

---

## 6. `TestHttpClient` surface

```csharp
var api = new TestHttpClient(httpClient, baseUrl: "api/v2/details/admin");      // + optional IResultRawFormat

Task<Result<T>>            Get<T>(string url = "", CancellationToken ct = default);
Task<Result<PagedList<T>>> GetList<T>(string url = "", CancellationToken ct = default);
Task<Result<T>>            Post<T>(string url, object body, CancellationToken ct = default);
Task<Result>               Post(string url, object body, CancellationToken ct = default);
Task<Result<T>>            Put<T>(string url, object body, CancellationToken ct = default);
Task<Result>               Put(string url, object body, CancellationToken ct = default);
Task<Result<T>>            Patch<T>(string url, object body, CancellationToken ct = default);
Task<Result>               Patch(string url, object body, CancellationToken ct = default);
Task<Result>               Delete(string url, CancellationToken ct = default);
Task<Result<T>>            Delete<T>(string url, CancellationToken ct = default);

public sealed record PagedList<T>(IReadOnlyList<T> Items, Pagination Pagination);  // Pagination reused from Vostra.Results.AspNetCore
```
- **No `where T : class`** — Core/STJ don't need it; keeping it would regress flexibility vs the old helper.
- **Created/201 recovery:** on a 2xx success the client reads the HTTP status — `201` → `Result.Created<T>(data)`
  / `Result.Created()`; otherwise the implicit `Ok` success. Round-trip preserves `SuccessKind` (uses the
  public `Result.Created<T>` factory).
- URL joining: `baseUrl` + `url` with documented single-slash normalization (improves on the reference's naive
  concat).
- `PagedList<T>` wraps the **reused** `Vostra.Results.AspNetCore.Pagination` — no re-declaration, so there is a
  single `Pagination` type across both packages (no ambiguous-reference clash in the round-trip tests).

---

## 7. Assertions (zero-dep, fluent) — FR-11.4 / S8

Extension methods on **both** `Result<T>`/`Result` **and** `Task<Result<T>>`/`Task<Result>` (so no stray
`await`), throwing `VostraAssertionException`.

```csharp
// reads like a domain script:
var product = await api.Post<Product>("", dto).ShouldBeSuccess();          // terminal → returns the value
await api.Get<Product>("/missing").ShouldHaveError(ErrorType.NotFound);    // by category
await api.Get<Product>("/missing").ShouldHaveError("Order.NotFound");      // by exact code
await api.Post<Product>("", bad).ShouldBeValidation();                     // kind sugar
await api.Get<Product>("/1").Assert(p => Assert.Equal("X", p.Name));       // assert success + body (S8), chainable
var page = await api.GetList<Product>().ShouldBeSuccess();                 // PagedList<T>: page.Items / page.Pagination
```

**Fluent grammar (locked) — one rule, no overlap:**
- **Chainable (return `Result<T>`):** `Assert(Action<T>)`, `ShouldHaveError(string code)`,
  `ShouldHaveError(ErrorType)`, and the kind sugar below. `Assert` additionally implies success (throws if the
  result is a failure), then runs the action — inner asserts throw.
- **Terminal (return `T`):** `ShouldBeSuccess()` asserts success and **returns the value**, ending the chain.
  (There is intentionally no `Result<T>`-returning `ShouldBeSuccess` — use `Assert(...)` to keep chaining.)
  On the **non-generic `Result`** there is no value: its `ShouldBeSuccess()` asserts success and returns the
  `Result` (chainable).
- Kind sugar (chainable): `ShouldBeNotFound()`, `ShouldBeValidation()`, `ShouldBeConflict()`,
  `ShouldBeUnauthorized()`, `ShouldBeForbidden()`, `ShouldBeUnexpected()` (each = `ShouldHaveError(ErrorType.X)`).
- **Semantics:** `ShouldHaveError(code|type)` matches if **any** error in the list matches (useful for
  multi-error validation).
- On failure, `VostraAssertionException.Message` is the S6 rich blob (verb, URL, request body, server error);
  the exception also carries the errors for debuggers. (Named with the `Vostra` prefix deliberately, to avoid
  an ambiguous-reference clash with NUnit's `AssertionException`.)

---

## 8. Worked round-trip (the symmetry, end to end)

```csharp
// service (Core):            return new NotFoundError($"Order {id} not found", "Order.NotFound");
// controller (AspNetCore):   return result.ToHttpResponse(HttpContext);     // → 404 problem+json with code
// test (Testing):
await api.Get<Order>($"/{id}").ShouldHaveError("Order.NotFound");           // ✅ no substring match (P2 fixed)
```

---

## 9. Testing approach

- **Round-trip (primary, usage-first):** an in-process minimal-API host via `WebApplicationFactory` whose
  endpoints route through the **real** `ToHttpResponse`; `TestHttpClient` consumes that actual HTTP response.
  Proves acceptance criterion #5 (`ShouldHaveError(...)` with no substring) and exercises every verb, success
  kind, list/pagination, and error shape end to end.
- **Unit (raw format):** `RawJsonFormat`/`ProblemDetailsErrorReader` against **canned JSON** for each shape —
  single error, multi-error array, validation map, valueless success, list, 201-vs-200, unknown `errorType`,
  non-JSON/empty body, and the S7 strict-deser hint.
- **Assertions:** pass/fail behavior, chaining grammar, async overloads, and the S6 message content.
- Built **subagent-driven TDD** (as Core/AspNetCore were). Coverage target per NFR-7.

---

## 10. Requirement coverage

| Req | How |
|-----|-----|
| FR-11.1 | `TestHttpClient` verbs returning `Result(<T>)`, fully async, baseUrl-injected |
| FR-11.2 | S6 rich message composed at throw-time; S7 strict-deser hint in `RawJsonFormat` |
| FR-11.3 | `ProblemDetailsErrorReader` rebuilds typed `ErrorBase` from `errorType`/`code` |
| FR-11.4 | zero-dep chainable `Assert`/`ShouldBeSuccess`/`ShouldHaveError` |
| FR-11.5 | `IResultRawFormat` seam (serializer + envelope), default STJ |
| FR-12   | consumes STJ JSON, enum-as-string, case-tolerant |
| S5–S8   | domain-script reads (S5), rich message (S6), strict guard+hint (S7), chainable `Assert` (S8) |
| P2/P12/P13 | typed-error asserts / fully async / module-agnostic |

---

## 11. Deferred to later slices

1. **`WebApplicationFactory` convenience adapter** (next slice) — drop-in over the same `TestHttpClient`.
2. **Newtonsoft `IResultRawFormat`** implementation (seam already present).
3. Migration of the reference repo's integration tests to this vocabulary (requirements §8 / M4).
```
