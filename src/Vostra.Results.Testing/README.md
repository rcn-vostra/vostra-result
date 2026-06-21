# Vostra.Results.Testing

Integration-testing toolkit for [Vostra.Results](https://www.nuget.org/packages/Vostra.Results). A
`TestHttpClient` collapses an HTTP round-trip into a `Result<T>`, rebuilding the **typed error** from the
`Vostra.Results.AspNetCore` response so tests assert error *identity*, not substrings.

The result: tests read like the domain script they're checking — no HTTP plumbing, no brittle
`Content.Should().Contain("not found")`, and a rich failure diagnostic when something actually breaks:

```csharp
var api = new TestHttpClient(httpClient, baseUrl: "products");

// success: returns the value
var product = await api.Get<Product>("/7").ShouldBeSuccess();

// failure: typed-error assertion, no substring matching
await api.Get<Product>("/9").ShouldHaveError("Product.NotFound");
await api.Post<Product>("", invalid).ShouldBeValidation();

// lists
var page = await api.GetList<Product>().ShouldBeSuccess();   // page.Items / page.Pagination
```

- Fully async (no `.Result`), verbs: `Get`/`GetList`/`Post`/`Put`/`Patch`/`Delete` (generic + valueless).
- Errors rebuilt from RFC 7807 `problem+json` into the matching `ErrorBase` kind, preserving `Code`.
- Assertions are zero-dependency and throw a `VostraAssertionException` with a rich diagnostic (verb, URL,
  request body, server error) composed only on failure.
- Swap the serializer/envelope by implementing `IResultRawFormat` (default: `RawJsonFormat`, System.Text.Json).
