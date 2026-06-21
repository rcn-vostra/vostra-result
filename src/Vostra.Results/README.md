# Vostra.Results

A lean, async-friendly, **dependency-free** `Result<T>` for .NET. Stop throwing exceptions for expected
failures and stop juggling success-flags and out-params: errors become ordinary, typed values the compiler
tracks for you. The happy path stays implicit, the failure path is typed, and there is no `.Value` that
hands you garbage on failure.

A method that can fail just returns `Result<T>` — and returns the value or the error directly, with no
`Result.Ok` / `Result.Fail` ceremony:

```csharp
public Result<Order> GetOrder(int id) =>
    _db.Find(id) is { } order
        ? order                                                       // T     -> success
        : new NotFoundError($"Order {id} not found", "Order.NotFound"); // Error -> failure
```

Consume it safely — the value is reachable only inside a branch that runs *because* it succeeded, so you
can't forget to check:

```csharp
string label = result.Match(onOk: o => $"#{o.Id}", onErr: errs => errs[0].Message);
if (result.TryGetValue(out var order)) { /* use order */ }
```

Compose without ever unwrapping. Each step runs only on success, the first error short-circuits the rest,
and async folds in with a single `await` — no `.Result`, no nested awaits:

```csharp
Result<Receipt> receipt =
    await GetOrderAsync(id)
        .Ensure(o => o.IsPaid, "Not paid.")   // false -> ValidationError
        .Then(o => ChargeAsync(o))            // T -> Task<Result<U>>, short-circuits on error
        .Map(r => r.Receipt);
```

When an operation can succeed as *one of several* shapes (not just value-or-error), say so in the type and
match every arm exhaustively — the compiler makes sure you handle each one:

```csharp
public Result<Pdf, Html> Render(Doc doc) =>
    doc.PrefersPrint ? RenderPdf(doc) : RenderHtml(doc);   // each arm (or an Error) converts implicitly

string mime = Render(doc).Match(
    pdf  => "application/pdf",
    html => "text/html",
    errs => errs[0].Code);                                 // error branch always present
```

What you get:

- `readonly struct`, zero happy-path allocation, **zero runtime dependencies**.
- Built-in error kinds (`ValidationError`, `NotFoundError`, `ConflictError`, `UnauthorizedError`,
  `ForbiddenError`, `Error`, …) with stable `Code` + neutral `ErrorType`; extend by subclassing `ErrorBase`.
- `Map` / `Then` / `Tap` / `Ensure` / `MapError`, full async matrix, LINQ (`from … select`), and `Combine`.
- Multi-success unions `Result<T1,T2>` / `Result<T1,T2,T3>` — one of several typed success shapes, or an
  error — consumed via exhaustive `Match`/`Switch`.
- Companion packages: **Vostra.Results.AspNetCore** (`Result` → HTTP) and **Vostra.Results.Testing**
  (HTTP → `Result` for integration tests).

**Full walkthrough:** see the [Usage Guide](https://github.com/rcn-vostra/vostra-result/blob/main/docs/usage.md).
