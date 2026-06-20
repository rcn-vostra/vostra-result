# Vostra.Results.AspNetCore

ASP.NET Core HTTP mapping for [Vostra.Results](https://www.nuget.org/packages/Vostra.Results): turn a
`Result`/`Result<T>` into an HTTP response with one extension method. Works in minimal APIs and MVC
controllers — no base class.

## Install

```bash
dotnet add package Vostra.Results.AspNetCore
```

## Setup (optional)

`ToHttpResponse` works with built-in status defaults out of the box. Call `AddVostraResults` only to
override the status map:

```csharp
builder.Services.AddVostraResults(o =>
{
    o.MapStatus(ErrorType.Conflict, 422);        // override a whole ErrorType
    o.MapStatusForCode("Order.Locked", 423);     // override one error code
});
```

Precedence: per-`Code` override → per-`ErrorType` map → built-in default.

| ErrorType | Default status |
|-----------|----------------|
| Validation | 400 |
| Unauthorized | 401 |
| Forbidden | 403 |
| NotFound | 404 |
| Conflict | 409 |
| Unexpected | 500 |

A new `ErrorBase` subclass maps automatically through its `ErrorType` — no mapping edits.

## Usage

```csharp
// minimal API
app.MapGet("/orders/{id}", (int id, HttpContext http, IOrderService svc) =>
    svc.Get(id).ToHttpResponse(http));

// controller
[HttpGet("{id}")]
public async Task<IResult> Get(int id) =>
    (await _svc.Get(id)).ToHttpResponse(HttpContext);

// paginated list
app.MapGet("/orders", (HttpContext http, IOrderService svc) =>
    svc.List().ToHttpResponse(http, new Pagination(page: 1, pageSize: 20, totalCount: 137)));
```

## Wire shapes

Success (`SuccessKind.Ok` → 200, `Created` → 201):

```json
{ "operationId": "00-abc…-01", "data": { "id": 42 } }
```

List:

```json
{ "operationId": "…", "data": [ … ],
  "pagination": { "page": 1, "pageSize": 20, "totalCount": 137, "totalPages": 7 } }
```

Error — RFC 7807 `application/problem+json`, carrying `code` + `errorType` so a test client can assert on
typed-error identity:

```json
{ "type": "about:blank", "title": "Not Found", "status": 404,
  "detail": "Order 42 not found", "code": "Order.NotFound", "errorType": "NotFound",
  "operationId": "…" }
```

Validation errors (all `ErrorType.Validation`) render a field → messages map (field key =
`metadata["field"]` if present, else the error `Code`):

```json
{ "status": 400, "code": "General.Validation", "errorType": "Validation",
  "errors": { "Sku": ["required"], "Price": ["must be > 0"] }, "operationId": "…" }
```

## Migration from a `{ status, operationId, data }` envelope

- Success bodies drop `status` (read the HTTP status line); error bodies keep it (RFC 7807).
- `operationId` is a W3C trace id (`Activity.Current?.Id ?? HttpContext.TraceIdentifier`).
- Lists use `ToHttpResponse(http, Pagination)` with this package's `Pagination` record.
