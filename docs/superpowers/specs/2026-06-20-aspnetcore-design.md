# Vostra.Results.AspNetCore — Design Spec

**Status:** Approved for planning · **Date:** 2026-06-20 · **Package:** `Vostra.Results.AspNetCore`

Maps `Result`/`Result<T>` onto HTTP via extension methods returning `IResult`. Builds on the completed
`Core` package. Implements FR-10 of
[result-type-requirements.md](../../../cc/docs/requirements/result-type-requirements.md); supersedes the
parked brainstorm [2026-06-20-aspnetcore-design-PARKED.md](../notes/2026-06-20-aspnetcore-design-PARKED.md).

## 1. Goal & scope

Provide the HTTP boundary for `Vostra.Results`: turn a service's `Result<T>` into an HTTP response with a
typed-error wire contract, **without** a controller base class and **without** putting any HTTP knowledge in
`Core`. The error *identity* (`code` + `ErrorType`) must survive onto the wire so the later `Testing` package
can reconstruct typed errors (FR-11.3).

**In scope:** `ToHttpResponse` extension methods (scalar, collection+pagination, non-generic), DI-configured
status mapping, RFC 7807 error envelope, thin success envelope, operation-id stamping, the shared
status↔code↔ErrorType contract that `Testing` will consume.

**Out of scope (v1):** global exception-handling middleware/filter; automatic model-binding-error
translation; any change to `Core`. These may follow in later specs.

## 2. Package & dependencies (NFR-1)

- New project `src/Vostra.Results.AspNetCore`, multi-targeting `net8.0;net9.0`.
- `FrameworkReference Include="Microsoft.AspNetCore.App"` (ASP.NET Core abstractions only).
- `ProjectReference` to `Vostra.Results` (Core).
- No other runtime NuGet dependencies. `Core` remains HTTP-free.

## 3. Public surface

Extension methods on `Result`/`Result<T>` returning `Microsoft.AspNetCore.Http.IResult`. No base class —
works identically in minimal APIs and MVC controllers. `HttpContext` is passed explicitly (testable; the
status map and operation id are read from `http.RequestServices` / `http.TraceIdentifier`).

```csharp
// Result<T> -> 200/201 success envelope, or error ProblemDetails
IResult ToHttpResponse<T>(this Result<T> result, HttpContext http);

// Result<IEnumerable<T>> -> list envelope with pagination, or error ProblemDetails
IResult ToHttpResponse<T>(this Result<IEnumerable<T>> result, HttpContext http, Pagination pagination);

// non-generic Result -> 200/201 (thin { operationId }), or error ProblemDetails
IResult ToHttpResponse(this Result result, HttpContext http);
```

Usage (both hosting styles must compile and read naturally):

```csharp
// minimal API
app.MapGet("/orders/{id}", (int id, HttpContext http, IOrderService svc) =>
    svc.Get(id).ToHttpResponse(http));

// controller
[HttpGet("{id}")]
public async Task<IResult> Get(int id) =>
    (await _svc.Get(id)).ToHttpResponse(HttpContext);
```

The collection overload accepts `Result<IEnumerable<T>>`; success status (200 vs 201) is read from the
result's `SuccessKind` exactly like the scalar overload.

**Overload resolution (must be explicit to avoid a silent mis-render).** The two overloads differ by arity
(the `Pagination` parameter), so they are unambiguous *per call*. The intended semantics, which the docs and
a test must pin down:

- A paginated list → call the 3-arg overload; you get the §5.2 list envelope (`data` array + `pagination`).
- A non-paginated `Result<IEnumerable<T>>` → the 2-arg scalar overload binds (with `T` = the enumerable)
  and renders `{ operationId, data: [ … ] }` — a plain array under `data`, **no** `pagination` key. This is
  a deliberate, valid rendering (an un-paginated list), **not** a bug.
- There is no compile error and no accidental binding: omitting `pagination` simply means "no pagination."
  Tests assert both shapes so the distinction stays intentional.

## 4. DI & status mapping (FR-10.2, P6)

```csharp
services.AddVostraResults(o =>
{
    o.MapStatus(ErrorType.Conflict, 422);      // override a whole ErrorType
    o.MapStatusForCode("Order.Locked", 423);   // override a single error Code
});
```

- `AddVostraResults` registers a configured `VostraResultsOptions` singleton.
- `ToHttpResponse` resolves the map via `http.RequestServices.GetService<VostraResultsOptions>()`. **If
  `AddVostraResults` was never called, it falls back to a default options instance** — the methods work out
  of the box.
- **Resolution precedence (must be documented):** per-`Code` override → per-`ErrorType` map → built-in
  default. First match wins.
- Adding a new `ErrorBase` subclass requires **zero** mapping edits: it maps through its `ErrorType`
  (FR-10.2 / acceptance §9.4).

### 4.1 Built-in default status map

`Core` defines six `ErrorType` values (the parked note's "7" was stale). Defaults:

| ErrorType    | HTTP status |
|--------------|-------------|
| Validation   | 400 |
| Unauthorized | 401 |
| Forbidden    | 403 |
| NotFound     | 404 |
| Conflict     | 409 |
| Unexpected   | 500 |

`ConflictError` and `AlreadyExistsError` both carry `ErrorType.Conflict` → both default to 409.

## 5. Wire contract (we own the JSON)

We construct the response bodies ourselves and serialize them, rather than delegating to ASP.NET's
problem-details generator. This fixes the exact wire shape across all hosting configurations, which is what
lets `Testing` deserialize it and rebuild typed errors (FR-11.3). Serialization is `System.Text.Json`
(OD-5 default), enum-as-string.

### 5.1 Success envelope (FR-10.4)

Thin; HTTP status line conveys 200 vs 201 (no `status` echoed in the body).

```json
{ "operationId": "00-abc…-01", "data": { "id": 42, "sku": "X-1" } }
```

Non-generic `Result` success → `{ "operationId": "…" }` (no `data`), status 200 or 201 from `SuccessKind`.

### 5.2 List envelope (FR-10.1)

```json
{
  "operationId": "00-abc…-01",
  "data": [ { "id": 1 }, { "id": 2 } ],
  "pagination": { "page": 1, "pageSize": 20, "totalCount": 137, "totalPages": 7 }
}
```

`Pagination` is an opinionated, self-contained record (the reference repo's `Pagination` was an external
NuGet we do not take):

```csharp
public sealed record Pagination(int Page, int PageSize, long TotalCount)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
```

### 5.3 Error envelope — RFC 7807 ProblemDetails (FR-10.4, OD-4)

Status from §4 resolution. Body uses the `Microsoft.AspNetCore.Mvc.ProblemDetails` POCO populated by us and
returned via `Results.Json(problem, statusCode: status, contentType: "application/problem+json")`:

```json
{ "type": "about:blank", "title": "Not Found", "status": 404,
  "detail": "Order 42 not found", "code": "Order.NotFound", "errorType": "NotFound",
  "operationId": "00-abc…-01" }
```

- `detail` = `error.Message`; `status` = resolved status.
- `code` (`error.Code`), `errorType` (`error.Type`, enum-as-string), and `operationId` go in
  `ProblemDetails.Extensions`.
- **We set `type`/`title`/content-type explicitly.** Because the body is hand-serialized via `Results.Json`
  (not `Results.Problem`/`ProblemDetailsService`), the framework does **not** populate RFC 7807 defaults:
  `ProblemResultBuilder` sets `type` = `"about:blank"`, `title` = the reason phrase for the status, and the
  `application/problem+json` content type itself. This is the cost of owning the exact wire shape (the
  deliberate trade chosen over `TypedResults.Problem`).

### 5.4 Multi-error collapse, non-validation (OD-2)

A non-validation `Result` carrying N errors: **first error sets status, `detail`, and `code`**; all errors
are listed in an `errors` extension array. (The §4 status resolution runs on the first error only.)

```json
{
  "type": "about:blank", "title": "Conflict", "status": 409,
  "detail": "Order is locked", "code": "Order.Locked", "errorType": "Conflict", "operationId": "…",
  "errors": [ { "code": "Order.Locked", "message": "Order is locked" },
              { "code": "Order.Stale",  "message": "Version mismatch" } ]
}
```

Note the `errors` extension is an **array of `{code,message}`** here, but an **object/map** in the
validation case (§5.5). The two shapes are intentional; the `Testing` deserializer branches on the
`errorType`/`status` to know which to expect.

### 5.5 Validation errors → ValidationProblemDetails (FR-11.3)

When **every** error in the result is `ErrorType.Validation`, render
`Microsoft.AspNetCore.Http.HttpValidationProblemDetails` (field → messages), status 400:

```json
{ "type": "about:blank", "title": "Bad Request", "status": 400,
  "code": "General.Validation", "errorType": "Validation", "operationId": "…",
  "errors": { "Sku": ["required"], "Price": ["must be > 0"] } }
```

- Rendered from a `HttpValidationProblemDetails` POCO (its `Errors` is `IDictionary<string,string[]>`),
  populated by us and returned via `Results.Json(..., contentType: "application/problem+json")` — `type`,
  `title`, and content-type set explicitly, exactly as §5.3.
- **Field key convention:** `error.Metadata?["field"]` (string) if present, otherwise `error.Code`.
- Value: the list of messages grouped under that key.
- No change to `Core`'s `ValidationError` — the field name rides in the existing optional metadata bag.
- `code` (first error) + `errorType` = `"Validation"` extensions carry through for symmetry with §5.3.

## 6. Operation id (FR-10.3)

`Activity.Current?.Id ?? http.TraceIdentifier`. No `CorrelationId` package dependency; a small private
helper inside `ToHttpResponseExtensions` computes it (not a standalone class). Stamped into every success and error body as `operationId`. (`System.Diagnostics.Activity`
rides in the `Microsoft.AspNetCore.App` shared framework, so NFR-1 still holds — no added NuGet reference.)

## 7. Shared status↔code↔ErrorType contract (FR-11.3)

The error envelope carries **both `code` and `errorType`** (§5.3), which is what makes typed reconstruction
possible. The contract is defined **once** here and exposed (public, or `InternalsVisibleTo` the Testing
assembly) so the serialize (here) and deserialize (Testing) sides cannot drift:

- **Forward (here):** `ErrorBase` → (status via §4, `code`, `errorType`) on the wire.
- **Inverse (Testing):** from the wire, `Testing` rebuilds an `ErrorBase` carrying the wire `code` and
  `errorType`. Tests assert on **`code`** (`ShouldHaveError("Order.NotFound")`) and/or `errorType`.

**Honest limit (do not over-claim):** the wire does **not** preserve the *concrete subclass*. The status
map is many-to-one (`ConflictError` and `AlreadyExistsError` both → `ErrorType.Conflict` → 409), and a
consumer `code` is a free string with no registered subclass. So FR-11.3 is satisfied at the granularity of
**(`code`, `errorType`)**, not concrete CLR type. `Testing` materializes a generic `ErrorBase` (or a
built-in subclass chosen from `errorType` where unambiguous) with the wire `code`/`errorType`/`message`;
assertions key on `code`/`errorType`, never on `is NotFoundError`. The `errorType` → representative
built-in subclass association (for the unambiguous types) lives in `DefaultStatusMap` and is the single
shared source both packages consume.

## 8. File layout (one responsibility each)

| File | Responsibility |
|------|----------------|
| `ToHttpResponseExtensions.cs` | the three `ToHttpResponse` overloads |
| `ServiceCollectionExtensions.cs` | `AddVostraResults(Action<VostraResultsOptions>)` |
| `VostraResultsOptions.cs` | status map state + `MapStatus` / `MapStatusForCode` |
| `ErrorStatusResolver.cs` | `ErrorBase` → status, applying §4 precedence |
| `ProblemResultBuilder.cs` | builds the error `IResult` (ProblemDetails / ValidationProblemDetails, single + multi) |
| `Envelopes.cs` | `SuccessEnvelope<T>`, `ListEnvelope<T>`, `Pagination` |
| `DefaultStatusMap.cs` | built-in `ErrorType` → status defaults (§3.x) + ErrorType↔subclass association |

The operation id (§6) is a **private helper inside `ToHttpResponseExtensions`**, not a standalone class
(dropped as over-engineering for a generic library; no `InternalsVisibleTo`).

## 9. Testing strategy

xUnit + FluentAssertions, **usage-first** (project convention: judge by call-site ergonomics, always write
usage-demonstrating tests):

- Execute each returned `IResult` against a `DefaultHttpContext` configured with `RequestServices` and a
  buffered response body; assert the HTTP status code and deserialize the body to verify shape, `code`,
  `operationId`, and the validation `errors` map.
- Demonstrate real call-sites: a minimal-API handler and a controller action both returning
  `result.ToHttpResponse(HttpContext)` compile and produce identical envelopes.
- Cover: 200 vs 201 (`SuccessKind`); each default status mapping; `MapStatus` and `MapStatusForCode`
  overrides + precedence; missing `AddVostraResults` falls back to defaults; single-error ProblemDetails;
  multi-error `errors[]`; validation field map (metadata field key vs code fallback); pagination envelope
  incl. `totalPages` edge cases (pageSize 0).
- The reference repo's integration tests remain the eventual end-to-end regression gate once the `Testing`
  package lands.

## 9a. Documentation deliverable

Usage docs ship with the package, not as an afterthought:

- A package `README.md` (`src/Vostra.Results.AspNetCore/README.md`, also used as the NuGet package readme)
  covering: install, `AddVostraResults` setup, the three `ToHttpResponse` overloads, minimal-API **and**
  controller call-site examples, the success/list/error/validation wire shapes, and the status-mapping
  precedence with a `MapStatus`/`MapStatusForCode` example.
- The repo root `README.md` gains an **AspNetCore** section (or table row) linking to the package readme,
  consistent with how Core is presented.
- `THIRD-PARTY-NOTICES.md` updated only if any code is lifted verbatim (none expected here; mapping is our
  own contribution per requirements §7).

## 9b. Migration notes (call out the breaking wire changes)

A mechanical swap from the legacy `AM.Extensions.AspNetCore` is **not** byte-compatible on the wire; the
docs and `README` must state these explicitly so existing clients aren't silently broken:

- **Success body drops `status`.** Legacy `{ status, operationId, data }` → new `{ operationId, data }`;
  the status lives only on the HTTP status line. Error bodies keep `status` (RFC 7807 requires it). Any
  client reading `response.status` on a *success* body must switch to the HTTP status code.
- **`operationId` format changes.** Legacy used a `CorrelationId` value; this package emits a W3C trace id
  (`00-…-01`) or `TraceIdentifier`. Same field name, different shape — note it for clients that parse it.
- The legacy generic `ToHttpResponse<T,P>(Result<IEnumerable<T>>, P)` becomes
  `ToHttpResponse<T>(Result<IEnumerable<T>>, Pagination)` with our own `Pagination` record (the external
  `VolvoCars.API.Common.Models.Pagination` is not taken).

## 10. Acceptance criteria

1. `result.ToHttpResponse(HttpContext)` compiles and works in both a minimal-API handler and a controller
   action with no base class (FR-10.1).
2. A new `ErrorBase` subclass gets a correct status with **zero** edits to mapping code (FR-10.2 / §9.4).
3. `SuccessKind.Created` yields 201, `Ok` yields 200 (FR-10.3).
4. Errors serialize as RFC 7807 ProblemDetails carrying `code` + `errorType` + `operationId` (with `type`,
   `title`, and `application/problem+json` content type set explicitly); validation errors as a
   field→messages map (FR-10.4).
4a. The error wire (`code` + `errorType`) is sufficient for `Testing` to assert `ShouldHaveError(code)` /
   by `errorType`, verified by a round-trip test in this package (FR-11.3 readiness).
5. Per-`code` override beats per-`ErrorType` map beats default (§4 precedence), verified by test.
6. Methods work without `AddVostraResults` (default map fallback).
7. Package references only `Microsoft.AspNetCore.App` + Core (NFR-1).
8. The status↔code↔ErrorType contract is exposed for `Testing` to consume (FR-11.3 readiness).
9. Package `README.md` and the repo-root `README.md` document install, setup, both call-site styles, and the
   wire shapes (§9a).

## 11. Open items deferred to later specs

- `Testing` package consuming §7 to rebuild typed errors and assert `ShouldHaveError(code)`.
- Optional global exception → ProblemDetails middleware.
- Retire vs wrap the old `AM.Extensions.AspNetCore` (requirements OD-6 / M1).
