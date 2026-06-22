# Vostra.Results — Usage Guide

Most C# error handling forces a bad trade: throw exceptions for expected failures (and pay for stack
unwinding, lose the compiler's help, and scatter `try/catch`), or return flags and out-parameters (and
reason about `null` everywhere). `Vostra.Results` takes a third path — **errors are ordinary values** that
the type system tracks for you. Three small packages share one idea: **the happy path is implicit, the
failure path is typed, and a value only exists where it's actually valid.**

- **Vostra.Results** — the `Result<T>` type itself. Zero dependencies, zero happy-path allocation.
- **Vostra.Results.AspNetCore** — turn a `Result` into the right HTTP response, automatically.
- **Vostra.Results.Testing** — transport-neutral chain & assert over any `Task<Result<T>>`; tests read like domain scripts.
- **Vostra.Results.AspNetCore.Testing** — turn an HTTP response back into a `Result` (the `TestHttpClient`).

```bash
dotnet add package Vostra.Results                    # the type
dotnet add package Vostra.Results.AspNetCore         # Result -> HTTP
dotnet add package Vostra.Results.Testing            # chain & assert over any Task<Result<T>> (tests)
dotnet add package Vostra.Results.AspNetCore.Testing # HTTP -> Result test client
```

---

## 1. Core — `Result<T>`

### Return a result — no factories

A method that can fail just declares `Result<T>` and returns either the value or an error. There's no
`Result.Ok(...)` / `Result.Fail(...)` ceremony to remember — the value and the error each convert
themselves, so the happy path looks exactly like ordinary code:

```csharp
public Result<Order> GetOrder(int id) =>
    _db.Find(id) is { } order
        ? order                                           // T            -> success
        : new NotFoundError($"Order {id} not found", "Order.NotFound");  // Error -> failure
```

Errors aren't strings — each is a typed value with a stable `Code`, a human `Message`, and a neutral
`ErrorType` (the category boundaries map to HTTP later). The built-ins cover the everyday cases
(each: `new XError(message, code?, causedBy?, metadata?)`):
`ValidationError`, `NotFoundError`, `ConflictError`, `AlreadyExistsError`, `UnauthorizedError`,
`ForbiddenError`, `Error` (unexpected/500).

Need a domain-specific failure? Subclass `ErrorBase` once and it behaves like any built-in — typed,
mappable to a status, assertable in tests:

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

There is deliberately **no** `.Value` property to read at the wrong time. You reach the value only inside a
branch that runs *because* the result succeeded — so "forgot to check `IsSuccess`" simply can't compile.
Pick whichever shape fits the call site:

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

Here's the real payoff: you keep composing *without ever unwrapping*. Each step runs only if the previous
one succeeded, and the first error short-circuits the rest — so the chain reads as the happy path while the
failure path is handled for free:

```csharp
Result<decimal> total =
    GetOrder(id)
        .Ensure(o => o.Items.Count > 0, "Order is empty.")   // false -> ValidationError (400)
        .Map(o => o.Total)                                   // T -> U
        .Tap(t => _log.Info($"total {t}"));                  // side-effect, passes through

Result<Receipt> receipt =
    GetOrder(id).Then(Charge);                               // T -> Result<U> (short-circuits on error)
```

The toolkit: `Map` (transform the value), `Then` (run another fallible step), `Tap`/`TapError`
(side-effects without breaking the chain), `Ensure` (guard → 400), `MapError` (re-tag errors).

### Async — one `await`, then chain

Async usually wrecks this style with `await` noise and `.Result` deadlocks. Here it doesn't. The chain
"lifts" to `Task<Result<T>>` on its first async step and stays there, so sync and async steps interleave
freely — **one** `await` at the front, no nesting, no sync-over-async anywhere:

```csharp
Result<Receipt> receipt =
    await GetOrderAsync(id)                  // Task<Result<Order>>
        .Ensure(o => o.IsPaid, "Not paid.")
        .Then(o => ChargeAsync(o))           // Func<T, Task<Result<U>>>
        .Map(r => r.Receipt);
```

### LINQ query syntax

Prefer a declarative shape? The same composition is available as a query. Each `from` only runs if the
previous one succeeded (so put your fetches *in* the query to skip the rest on failure), and `where`
becomes a validation guard:

```csharp
Result<Invoice> invoice =
    from customer in GetCustomer(id)
    from cart     in GetCart(customer.Id)
    where cart.Items.Count > 0               // false -> ValidationError
    select new Invoice(customer, cart);
```

### Combine many / validate-all

When you have several *independent* results and want **every** failure at once — the classic "tell me all
the bad fields", not "fix one, resubmit, repeat" — `Combine` accumulates them. Same tool scales to a
throttled async fan-out:

```csharp
Result<IReadOnlyList<Order>> all = Result.Combine(r1, r2, r3);   // all values, or ALL errors accumulated
Result combined                  = Result.Combine(v1, v2);       // valueless
Result<IReadOnlyList<Saved>> saved =
    await items.SelectAsync(i => SaveAsync(i), maxConcurrency: 4); // throttled fan-out, then combine
```

### Keep every outcome (don't collapse)

`Combine`/`SelectAsync` are *all-or-nothing*: one failure discards every success. When you instead need
to **record every per-item outcome** — the successes *and* the failures — and decide once over the whole
batch, reach for `SelectResultsAsync`. It's the non-collapsing sibling of `Combine`: same throttle, but it
hands back every `Result<T>` in input order so you summarize however you like:

```csharp
// run each item (throttled), keep every per-item Result<T> — successes AND failures
IReadOnlyList<Result<Handled>> outcomes =
    await batch.SelectResultsAsync(HandleAsync, maxConcurrency: 4, ct);

var failures = outcomes.Where(r => r.IsError).ToArray();   // nothing discarded
bool accept  = failures.Length == 0;

// pair back to inputs when the outcome doesn't carry identity (order is guaranteed):
foreach (var (item, outcome) in batch.Zip(outcomes))
    outcome.Switch(ok => Audit(item, ok), errs => Audit(item, errs[0].Code));
```

`SelectAsync` is just `SelectResultsAsync(...).Combine()` — use it when you want the single combined
result; use `SelectResultsAsync` when the per-item detail *is* the point.

### 200 vs 201

A success can mean "here it is" or "I just created it" — and that distinction should reach the wire as
200 vs 201 without a second return type. Flag the created case and the HTTP layer does the rest:

```csharp
return order;                  // success (Ok)
return Result.Created(order);  // success (Created) — maps to HTTP 201 later
```

### One of several success shapes — `Result<T1,T2>` / `Result<T1,T2,T3>`

Sometimes an operation succeeds as *one of several distinct value types* — not "a value or an error", but
"**this** value, or **that** value, or an error". Instead of a shared base type or an `object` (which throw
away static typing), reach for the multi-success overloads — each arm stays its own type:

```csharp
public Result<Pdf, Html> Render(Doc doc) =>
    doc.PrefersPrint
        ? RenderPdf(doc)                                 // T1   -> success (arm 1)
        : RenderHtml(doc);                               // T2   -> success (arm 2)
        // return new NotFoundError($"doc {doc.Id}");    // Error -> failure, same channel as Result<T>
```

Every arm and the error channel convert implicitly — no factories. You consume by matching **every** arm
exhaustively, so the compiler guarantees you've handled each shape (the error branch is always present and
receives the full error list):

```csharp
IResult response = Render(doc).Match(
    pdf  => Results.File(pdf.Bytes, "application/pdf"),
    html => Results.Content(html.Markup, "text/html"),
    errs => Results.Problem(errs[0].Message));

Render(doc).Switch(                                      // void counterpart
    pdf  => _sink.Write(pdf),
    html => _sink.Write(html),
    errs => _log.Warn(errs[0].Message));

int active = Render(doc).Index;                          // 1 or 2 on success; 0 when faulted
```

`Result<T1,T2,T3>` is the same with a third arm. Both are `readonly struct`s with the same zero-allocation,
no-`.Value`-footgun, `default`-is-faulted discipline as `Result<T>`; value-type arms don't box.

**It's a terminal match surface, by design.** You *produce* one (return it) and *match out* of it — there
is intentionally no `Map`/`Then`/LINQ *through* a union (which arm would it transform?). Chain into the
union with `Result<T>` combinators, then `Match` at the boundary.

When two arms share a type, implicit conversion is ambiguous — select the arm explicitly:

```csharp
Result<int, int> a = Result<int, int>.First(0);    // arm 1
Result<int, int> b = Result<int, int>.Second(0);   // arm 2  (a != b)
```

> The error channel is identical to `Result<T>`, so a union flows through an ASP.NET Core endpoint today
> via `Match` (map each success arm to a response yourself). Dedicated `ToHttpResponse`/testing support for
> unions is intentionally deferred — see the design spec.

---

## 2. Vostra.Results.AspNetCore — `Result` → HTTP

This is where errors-as-values pays off at the edge: one call turns any `Result` into the correct HTTP
response — success envelope or RFC 7807 `ProblemDetails` — with the status derived from the error itself.
No per-endpoint `try/catch`, no central status `switch` to edit every time you add an error kind.

### Setup (optional)

You can wire it explicitly to customize behavior, but it works out of the box with sensible defaults:

```csharp
builder.Services.AddVostraResults();   // optional; falls back to sensible defaults if omitted
```

### Return from endpoints

Your endpoint stays one line: return the `Result` through `ToHttpResponse` and let the mapping decide the
status, body, and (for collections) pagination:

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

Success carries an `operationId` for correlation; failures are standard `ProblemDetails` that keep the
error's **identity** (`code` + `errorType`) on the wire — not just a message:

```jsonc
// success            { "operationId": "…", "data": { … } }
// list               { "operationId": "…", "data": [ … ], "pagination": { "page":1, "pageSize":20, "totalCount":57, "totalPages":3 } }
// error (RFC 7807)   { "status":404, "detail":"Order 7 not found", "code":"Order.NotFound", "errorType":"NotFound", … }
```

That `code` + `errorType` is the bridge to §3: because the identity survives the round-trip, tests can
reconstruct the **typed** error instead of grepping the message.

### Customize status codes (open/closed — no central switch)

Mapping is configuration, not code you maintain. Override a category or a specific code; everything else
keeps its default. Adding a brand-new error kind needs **zero** changes here:

```csharp
builder.Services.AddVostraResults(o => o
    .MapStatus(ErrorType.Conflict, StatusCodes.Status409Conflict)
    .MapStatusForCode("Order.NotFound", StatusCodes.Status410Gone));   // precedence: code -> type -> default
```

---

## 3. Vostra.Results.Testing — chain & assert over any `Task<Result<T>>`

The test-composition layer is **transport-neutral**: `Then` (from Core) chains steps that each return
`Result<T>`/`Task<Result<T>>`, running the next only if the previous succeeded; the `ShouldBe…`/`Assert`
helpers assert outcomes by **identity** (code or `ErrorType`), not substring. It depends only on the core
type — point it at an HTTP call, a queue round-trip, or any fallible async operation.

```csharp
// any function returning Task<Result<T>> composes — here a non-HTTP transport:
var settled = await SendAndAwaitAsync(workOrder)        // your transport -> Task<Result<State>>
    .Then(state => AdvanceAsync(state))                 // runs only if the send settled OK
    .Assert(s => s.Status.Should().Be(Status.Applied))  // inline checkpoint
    .ShouldBeSuccess();                                 // terminal: returns the value or throws
```

Attach a `RequestContext` to your errors for the same rich failure diagnostics the HTTP client gets:

```csharp
return new ExternalRefusalError("contractor rejected line 4")
    .WithRequestContext(new RequestContext("SEND", "wo-inbound", workOrder));
// an assertion failure then renders:  request: SEND wo-inbound | body: WorkOrder { ... }
```

### HTTP adapter — `Vostra.Results.AspNetCore.Testing`

For ASP.NET Core APIs, the `Vostra.Results.AspNetCore.Testing` package adds `TestHttpClient`, which collapses
the HTTP round-trip back into a `Result<T>` and **rebuilds the typed error** from the RFC 7807 body — so the
same chain-and-assert layer reads as a domain script over your endpoints, asserting on identity instead of
brittle `response.Content.Should().Contain("not found")` substring checks.

### Setup

Point a `TestHttpClient` at your API (real `HttpClient` or an in-process test server) and you're done:

```csharp
var api = new TestHttpClient(httpClient, baseUrl: "api/products");
```

Verbs (all async, generic + valueless): `Get<T>` · `GetList<T>` · `Post<T>`/`Post` · `Put<T>`/`Put` ·
`Patch<T>`/`Patch` · `Delete`/`Delete<T>`.

### Assert outcomes — typed, no substring matching

Assert what actually matters — the outcome's identity — by code or by category. `ShouldBeSuccess` returns
the value so you can keep going; the others read as plain statements of intent:

```csharp
var product = await api.Get<Product>("/7").ShouldBeSuccess();    // terminal: returns the value
await api.Get<Product>("/9").ShouldHaveError("Order.NotFound");  // by exact code
await api.Get<Product>("/9").ShouldHaveError(ErrorType.NotFound);// by category
await api.Post<Product>("", invalid).ShouldBeValidation();       // kind sugar
await api.Get<Product>("/7").Assert(p => p.Name.Should().Be("Widget")); // inline check, chainable
```

When something does fail, you get a `VostraAssertionException` with a rich diagnostic (verb, URL, request
body, server error) — built only on actual failure, so passing tests pay nothing for it.

### Chain dependent requests

Multi-step flows compose just like the core type. `Then` runs the next request **only if the previous
succeeded** and short-circuits on the first failure, so a single terminal assertion reports whatever broke,
wherever it broke, with full diagnostics:

```csharp
var detailed = await api.Post<ProductResponse>("/products/", create1)
    .Then(_       => api.Post<ProductResponse>("/products/", create2))           // runs after create1 OK
    .Then(created => api.Get<ProductDetailedResponse>($"/products/{created.Id}"))// runs after create2 OK
    .ShouldBeSuccess();                                                          // throws if ANY step failed

detailed.Name.Should().Be(create2.Name);
```

Prefer a checkpoint at each step instead of one at the end? `Assert()` confirms success and passes the
result through; `Assert(x => …)` also inspects the value:

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

List endpoints come back as a first-class result with the values *and* the pagination metadata, both
ready to assert:

```csharp
var page = await api.GetList<Product>().ShouldBeSuccess();
page.Items.Should().HaveCount(2);
page.Pagination.TotalCount.Should().Be(2);
```

### Spin up an in-process API and test the real round-trip

No mocks, no separate process — host the real endpoints in-memory and exercise the genuine wire contract,
typed assertion and all:

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

Not on System.Text.Json, or use a different envelope shape? The format is injectable — implement
`IResultRawFormat` (e.g. for Newtonsoft) and pass it in; nothing else changes:

```csharp
// default is RawJsonFormat (System.Text.Json):
var api = new TestHttpClient(client, baseUrl, myFormat);
```

---

## Why it reads well

- **Happy path is implicit** — `return value;` and `return new NotFoundError(...);` both compile.
- **Failure is typed and survives the wire** — the same error identity flows service → HTTP → test assertion.
- **Async is first-class** — one `await`, then `Map`/`Then`/`Ensure` interleave freely.
- **Boundaries collapse into `Result`** — one place maps to HTTP; tests read as domain scripts.
