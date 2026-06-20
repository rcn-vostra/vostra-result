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
{ "status": 404, "detail": "Order 42 not found", "code": "Order.NotFound", "operationId": "00-abc…-01" }
```

- `detail` = `error.Message`; `status` = resolved status.
- `code` (`error.Code`) and `operationId` go in `ProblemDetails.Extensions`.
- `type`/`title` left at framework defaults (RFC 7807 `type` = `about:blank`).

### 5.4 Multi-error collapse, non-validation (OD-2)

A non-validation `Result` carrying N errors: **first error sets status, `detail`, and `code`**; all errors
are listed in an `errors` extension array. (The §4 status resolution runs on the first error only.)

```json
{
  "status": 409, "detail": "Order is locked", "code": "Order.Locked", "operationId": "…",
  "errors": [ { "code": "Order.Locked", "message": "Order is locked" },
              { "code": "Order.Stale",  "message": "Version mismatch" } ]
}
```

### 5.5 Validation errors → ValidationProblemDetails (FR-11.3)

When **every** error in the result is `ErrorType.Validation`, render
`Microsoft.AspNetCore.Http.HttpValidationProblemDetails` (field → messages), status 400:

```json
{ "status": 400, "code": "General.Validation", "operationId": "…",
  "errors": { "Sku": ["required"], "Price": ["must be > 0"] } }
```

- **Field key convention:** `error.Metadata?["field"]` (string) if present, otherwise `error.Code`.
- Value: the list of messages grouped under that key.
- No change to `Core`'s `ValidationError` — the field name rides in the existing optional metadata bag.
- A `code` extension carries the first error's code for symmetry with §5.3.

## 6. Operation id (FR-10.3)

`Activity.Current?.Id ?? http.TraceIdentifier`. No `CorrelationId` package dependency; a small internal
helper computes it. Stamped into every success and error body as `operationId`.

## 7. Shared status↔code↔ErrorType contract (FR-11.3)

The mapping that turns an `ErrorBase` into (status, code) — and its inverse used by `Testing` to pick the
built-in `ErrorBase` subclass for a given status — is defined **once** in this package and exposed so
`Testing` consumes it rather than re-deriving it. Concretely: the default status map (§4.1) and the
`ErrorType` → built-in-subclass association are public (or `InternalsVisibleTo` the Testing assembly). This
prevents the serialize (here) and deserialize (Testing) sides from drifting.

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
| `OperationId.cs` | internal `Activity.Current?.Id ?? TraceIdentifier` helper |

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

## 10. Acceptance criteria

1. `result.ToHttpResponse(HttpContext)` compiles and works in both a minimal-API handler and a controller
   action with no base class (FR-10.1).
2. A new `ErrorBase` subclass gets a correct status with **zero** edits to mapping code (FR-10.2 / §9.4).
3. `SuccessKind.Created` yields 201, `Ok` yields 200 (FR-10.3).
4. Errors serialize as RFC 7807 ProblemDetails carrying `code` + `operationId`; validation errors as a
   field→messages map (FR-10.4).
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
