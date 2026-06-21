# Vostra.Results

A lean, async-friendly, **dependency-free** `Result<T>` for .NET. The happy path is implicit, the failure
path is typed, and there is no `.Value` that returns garbage on failure.

```csharp
public Result<Order> GetOrder(int id) =>
    _db.Find(id) is { } order
        ? order                                                       // T     -> success
        : new NotFoundError($"Order {id} not found", "Order.NotFound"); // Error -> failure
```

Consume without a footgun:

```csharp
string label = result.Match(onOk: o => $"#{o.Id}", onErr: errs => errs[0].Message);
if (result.TryGetValue(out var order)) { /* use order */ }
```

Transform and chain — one `await`, then sync/async steps interleave:

```csharp
Result<Receipt> receipt =
    await GetOrderAsync(id)
        .Ensure(o => o.IsPaid, "Not paid.")   // false -> ValidationError
        .Then(o => ChargeAsync(o))            // T -> Task<Result<U>>, short-circuits on error
        .Map(r => r.Receipt);
```

Succeed as *one of several* shapes — `Result<T1,T2>` / `Result<T1,T2,T3>` — and match exhaustively:

```csharp
public Result<Pdf, Html> Render(Doc doc) =>
    doc.PrefersPrint ? RenderPdf(doc) : RenderHtml(doc);   // each arm (or an Error) converts implicitly

string mime = Render(doc).Match(
    pdf  => "application/pdf",
    html => "text/html",
    errs => errs[0].Code);                                 // error branch always present
```

- `readonly struct`, zero happy-path allocation, **zero runtime dependencies**.
- Built-in error kinds (`ValidationError`, `NotFoundError`, `ConflictError`, `UnauthorizedError`,
  `ForbiddenError`, `Error`, …) with stable `Code` + neutral `ErrorType`; extend by subclassing `ErrorBase`.
- `Map` / `Then` / `Tap` / `Ensure` / `MapError`, full async matrix, LINQ (`from … select`), and `Combine`.
- Multi-success unions `Result<T1,T2>` / `Result<T1,T2,T3>` — one of several typed success shapes, or an
  error — consumed via exhaustive `Match`/`Switch`.
- Companion packages: **Vostra.Results.AspNetCore** (`Result` → HTTP) and **Vostra.Results.Testing**
  (HTTP → `Result` for integration tests).

**Full walkthrough:** see the [Usage Guide](https://github.com/rcn-vostra/vostra-result/blob/main/docs/usage.md).
