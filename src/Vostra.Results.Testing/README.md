# Vostra.Results.Testing

Transport-neutral testing toolkit for [Vostra.Results](https://www.nuget.org/packages/Vostra.Results) —
the fluent **chain-and-assert** layer, over **any** `Task<Result<T>>`. Depends only on the core type (no
ASP.NET Core), so it works against an HTTP call, a queue round-trip, or any fallible async operation.

```csharp
// any function returning Task<Result<T>> composes:
var settled = await SendAndAwaitAsync(workOrder)        // your transport -> Task<Result<State>>
    .Then(state => AdvanceAsync(state))                 // runs only if the previous step succeeded
    .Assert(s => s.Status.Should().Be(Status.Applied))  // inline checkpoint
    .ShouldBeSuccess();                                 // terminal: returns the value or throws
```

- **Chain:** `Then` (from Core) runs the next step only if the previous succeeded; the first failure
  short-circuits, so one terminal assertion reports whatever broke, wherever it broke.
- **Assert by identity:** `ShouldBeSuccess` / `ShouldHaveError(code | ErrorType)` / `Assert` /
  `ShouldBeNotFound` / `ShouldBeValidation` / … — never substring matching.
- **Rich diagnostics:** failures throw a `VostraAssertionException` composed only on failure. Attach a
  `RequestContext` to your errors — `error.WithRequestContext(new RequestContext("SEND", "queue", body))` —
  to get the same "what was attempted" line on any transport.

For ASP.NET Core APIs, add
[Vostra.Results.AspNetCore.Testing](https://www.nuget.org/packages/Vostra.Results.AspNetCore.Testing) — a
`TestHttpClient` that turns the HTTP round-trip back into a `Result<T>` (typed error rebuilt from RFC 7807)
and feeds this same chain-and-assert layer.
