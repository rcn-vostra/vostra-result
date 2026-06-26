# Vostra.Result

A lean, async-friendly, dependency-free `Result<T>` type for .NET — plus ASP.NET Core mapping and an
integration-testing toolkit. Released under MIT.

Expected failures shouldn't need exceptions (slow, easy to forget, invisible to the compiler) or
hand-rolled success-flags (back to reasoning about `null`). `Vostra.Result` makes failure a **typed value**
the compiler tracks for you — the happy path reads like ordinary code, and the value only exists where it's
valid:

```csharp
public Result<Order> GetOrder(int id) =>
    _db.Find(id) is { } order
        ? order                                            // success — just return it
        : Result.NotFoundError($"Order {id} not found");   // failure — typed, no throw

string label = GetOrder(id).Match(o => $"#{o.Id}", errs => errs[0].Message);
```

That same typed failure then flows, unchanged, all the way to the HTTP wire and back into your tests.

## 📖 [Usage Guide](docs/usage.md)

Start here — a short, example-first walkthrough of all four packages (returning results, `Match`/`Then`/LINQ,
async chaining, multi-success unions `Result<T1,T2[,T3]>`, HTTP mapping, and typed-error integration testing
incl. chaining dependent requests).

## Vostra.Result

The core `Result<T>` type: a `readonly struct` with zero happy-path allocation and zero runtime
dependencies, implicit value/error conversions, exhaustive `Match`/`Switch`, the full sync+async combinator
matrix, LINQ, and multi-success unions. See [the package README](src/Vostra.Result/README.md).

## Vostra.Result.AspNetCore

One extension method turns a `Result`/`Result<T>` into the right HTTP response: a thin success envelope or
an RFC 7807 `ProblemDetails` error carrying `code` + `errorType`. The status comes from the error itself via
a DI-configured, open/closed map — so a new error kind needs no mapping edits. See
[src/Vostra.Result.AspNetCore/README.md](src/Vostra.Result.AspNetCore/README.md).

## Vostra.Result.Testing

Transport-neutral testing toolkit: the fluent **chain-and-assert** layer over **any** `Task<Result<T>>` —
`Then` chaining plus zero-dependency fluent assertions (`ShouldBeSuccess`, `ShouldHaveError`, kind sugar) and
`WithRequestContext` diagnostics, so tests assert identity, not substrings. Core-only (no ASP.NET Core dep).
See [the package README](src/Vostra.Result.Testing/README.md).

## Vostra.Result.AspNetCore.Testing

HTTP integration-testing toolkit built on `Vostra.Result.Testing`: a `TestHttpClient` that collapses the
HTTP round-trip back into a `Result<T>` with the **typed error** rebuilt from the RFC 7807 response — so
HTTP tests read like the domain script they check. See
[the package README](src/Vostra.Result.AspNetCore.Testing/README.md).
