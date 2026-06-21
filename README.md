# Vostra.Results

A lean, async-friendly, dependency-free `Result<T>` type for .NET — plus ASP.NET Core mapping and an integration-testing toolkit. Released under MIT.

## Vostra.Results.AspNetCore

HTTP mapping for `Result`/`Result<T>`: `result.ToHttpResponse(HttpContext)` returns an `IResult` with a thin
success envelope or an RFC 7807 `ProblemDetails` error carrying `code` + `errorType`. Status comes from the
error via a DI-configured, open/closed map. See
[src/Vostra.Results.AspNetCore/README.md](src/Vostra.Results.AspNetCore/README.md).

## Vostra.Results.Testing

Integration-testing toolkit: a `TestHttpClient` returning `Result<T>` with typed-error reconstruction, plus
zero-dependency fluent assertions (`ShouldBeSuccess`, `ShouldHaveError`, kind sugar). See
[the package README](src/Vostra.Results.Testing/README.md).
