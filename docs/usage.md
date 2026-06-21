# Vostra.Results — Usage Guide

A lean, async-friendly, dependency-free `Result<T>` for .NET, plus ASP.NET Core mapping and an
integration-testing toolkit. Three packages, one idea: **errors are values, the happy path is implicit,
and the value only exists where it's valid.**

```bash
dotnet add package Vostra.Results            # the type
dotnet add package Vostra.Results.AspNetCore # Result -> HTTP
dotnet add package Vostra.Results.Testing    # HTTP  -> Result (tests)
```

---

## 1. Core — `Result<T>`

### Return a result — no factories

```csharp
public Result<Order> GetOrder(int id) =>
    _db.Find(id) is { } order
        ? order                                           // T            -> success
        : new NotFoundError($"Order {id} not found", "Order.NotFound");  // Error -> failure
```

Built-in error kinds (each: `new XError(message, code?, causedBy?, metadata?)`):
`ValidationError`, `NotFoundError`, `ConflictError`, `AlreadyExistsError`, `UnauthorizedError`,
`ForbiddenError`, `Error` (unexpected/500). Every error carries a stable `Code`, a `Message`, and a
neutral `ErrorType`.

Make your own kind by subclassing `ErrorBase`:

```csharp
public sealed class PaymentDeclinedError : ErrorBase
{
    public PaymentDeclinedError(string message, Exception? causedBy = null,
        IReadOnlyDictionary<string, object?>? metadata = null)
        : base("Payment.Declined", message, ErrorType.Conflict, causedBy, metadata) { }

    protected override ErrorBase CloneWith(string code, string message, Exception? causedBy,
        IReadOnlyDictionary<string, object?>? metadata) => new PaymentDeclinedError(message, causedBy, metadata);
}
```

### Consume — no `.Value` footgun

```csharp
string label = result.Match(
    onOk:  order  => $"#{order.Id}",
    onErr: errors => errors[0].Message);

if (result.TryGetValue(out var order)) { /* use order */ }

Order order = result.GetValueOr(Order.Empty);

result.Switch(
    onOk:  o      => Console.WriteLine(o.Id),
    onErr: errs   => Log(errs));
```

### Transform & chain (sync)

```csharp
Result<decimal> total =
    GetOrder(id)
        .Ensure(o => o.Items.Count > 0, "Order is empty.")   // false -> ValidationError (400)
        .Map(o => o.Total)                                   // T -> U
        .Tap(t => _log.Info($"total {t}"));                  // side-effect, passes through

Result<Receipt> receipt =
    GetOrder(id).Then(Charge);                               // T -> Result<U> (short-circuits on error)
```

`Map` (functor), `Then` (monadic bind), `Tap`/`TapError` (side-effects), `Ensure` (guard → 400),
`MapError` (re-tag errors).

### Async — one `await`, then chain

The chain "lifts" to `Task<Result<T>>` on its first async step and stays there. Sync and async steps
interleave with no `.Result`, no nested `await`:

```csharp
Result<Receipt> receipt =
    await GetOrderAsync(id)                  // Task<Result<Order>>
        .Ensure(o => o.IsPaid, "Not paid.")
        .Then(o => ChargeAsync(o))           // Func<T, Task<Result<U>>>
        .Map(r => r.Receipt);
```

### LINQ query syntax

```csharp
Result<Invoice> invoice =
    from customer in GetCustomer(id)
    from cart     in GetCart(customer.Id)
    where cart.Items.Count > 0               // false -> ValidationError
    select new Invoice(customer, cart);
```

### Combine many / validate-all

```csharp
Result<IReadOnlyList<Order>> all = Result.Combine(r1, r2, r3);   // all values, or ALL errors accumulated
Result combined                  = Result.Combine(v1, v2);       // valueless
Result<IReadOnlyList<Saved>> saved =
    await items.SelectAsync(i => SaveAsync(i), maxConcurrency: 4); // throttled fan-out, then combine
```

### 200 vs 201

```csharp
return order;                  // success (Ok)
return Result.Created(order);  // success (Created) — maps to HTTP 201 later
```

---

## 2. Vostra.Results.AspNetCore — `Result` → HTTP

### Setup (optional)

```csharp
builder.Services.AddVostraResults();   // optional; falls back to sensible defaults if omitted
```

### Return from endpoints

```csharp
app.MapGet("/orders/{id}", (int id, HttpContext http) =>
    GetOrder(id).ToHttpResponse(http));                                  // 200 + body, or 404 problem+json

app.MapPost("/orders", (CreateOrder dto, HttpContext http) =>
    Create(dto).ToHttpResponse(http));                                   // 201 when Result.Created(...)

app.MapGet("/orders", (HttpContext http) =>
{
    Result<IEnumerable<Order>> page = Query();
    return page.ToHttpResponse(http, new Pagination(1, 20, total));      // list + pagination
});

app.MapDelete("/orders/{id}", (int id, HttpContext http) =>
    Delete(id).ToHttpResponse(http));                                    // non-generic Result -> 200/204-style
```

### What goes on the wire

```jsonc
// success            { "operationId": "…", "data": { … } }
// list               { "operationId": "…", "data": [ … ], "pagination": { "page":1, "pageSize":20, "totalCount":57, "totalPages":3 } }
// error (RFC 7807)   { "status":404, "detail":"Order 7 not found", "code":"Order.NotFound", "errorType":"NotFound", … }
```

The `code` + `errorType` on the error envelope are what let tests reconstruct the **typed** error (§3).

### Customize status codes (open/closed — no central switch)

```csharp
builder.Services.AddVostraResults(o => o
    .MapStatus(ErrorType.Conflict, StatusCodes.Status409Conflict)
    .MapStatusForCode("Order.NotFound", StatusCodes.Status410Gone));   // precedence: code -> type -> default
```

Adding a new error kind needs **zero** changes here.

---

## 3. Vostra.Results.Testing — HTTP → `Result`

Collapses an HTTP round-trip back into a `Result<T>`, rebuilding the **typed error** so tests assert
identity, not substrings.

### Setup

```csharp
var api = new TestHttpClient(httpClient, baseUrl: "api/products");
```

Verbs (all async, generic + valueless): `Get<T>` · `GetList<T>` · `Post<T>`/`Post` · `Put<T>`/`Put` ·
`Patch<T>`/`Patch` · `Delete`/`Delete<T>`.

### Assert outcomes — typed, no substring matching

```csharp
var product = await api.Get<Product>("/7").ShouldBeSuccess();    // terminal: returns the value
await api.Get<Product>("/9").ShouldHaveError("Order.NotFound");  // by exact code
await api.Get<Product>("/9").ShouldHaveError(ErrorType.NotFound);// by category
await api.Post<Product>("", invalid).ShouldBeValidation();       // kind sugar
await api.Get<Product>("/7").Assert(p => p.Name.Should().Be("Widget")); // inline check, chainable
```

On failure, `VostraAssertionException` carries a rich diagnostic (verb, URL, request body, server error) —
composed only when an assertion actually fails.

### Chain dependent requests

`Then` runs the next request **only if the previous succeeded** and short-circuits on the first error, so
one terminal assertion reports any failure along the way with full diagnostics:

```csharp
var detailed = await api.Post<ProductResponse>("/products/", create1)
    .Then(_       => api.Post<ProductResponse>("/products/", create2))           // runs after create1 OK
    .Then(created => api.Get<ProductDetailedResponse>($"/products/{created.Id}"))// runs after create2 OK
    .ShouldBeSuccess();                                                          // throws if ANY step failed

detailed.Name.Should().Be(create2.Name);
```

Want a check at each boundary instead of one at the end? `Assert()` confirms success and passes the result
through; `Assert(x => …)` also inspects the value:

```csharp
await api.Post<ProductResponse>("/products/", create1)
    .Assert()                                                     // create1 succeeded
    .Then(_ => api.Post<ProductResponse>("/products/", create2))
    .Assert()                                                     // create2 succeeded
    .Then(p => api.Get<ProductDetailedResponse>($"/products/{p.Id}"))
    .Assert(p =>                                                  // fetched — now inspect the body
    {
        p.Id.Should().Be(create2.Id);
        p.Name.Should().Be(create2.Name);
    });
```

### Lists & pagination

```csharp
var page = await api.GetList<Product>().ShouldBeSuccess();
page.Items.Should().HaveCount(2);
page.Pagination.TotalCount.Should().Be(2);
```

### Spin up an in-process API and test the real round-trip

```csharp
var builder = WebApplication.CreateSlimBuilder();
builder.WebHost.UseTestServer();
builder.Services.AddVostraResults();
var app = builder.Build();
app.MapGet("/products/{id}", (int id, HttpContext http) => GetProduct(id).ToHttpResponse(http));
await app.StartAsync();

var api = new TestHttpClient(app.GetTestClient(), "products");
await api.Get<Product>("/9").ShouldHaveError("Product.NotFound");   // real wire, typed assertion
```

### Swap the serializer / envelope

The default is `RawJsonFormat` (System.Text.Json). Implement `IResultRawFormat` to use Newtonsoft or a
different envelope, and pass it: `new TestHttpClient(client, baseUrl, myFormat)`.

---

## Why it reads well

- **Happy path is implicit** — `return value;` and `return new NotFoundError(...);` both compile.
- **Failure is typed and survives the wire** — the same error identity flows service → HTTP → test assertion.
- **Async is first-class** — one `await`, then `Map`/`Then`/`Ensure` interleave freely.
- **Boundaries collapse into `Result`** — one place maps to HTTP; tests read as domain scripts.
