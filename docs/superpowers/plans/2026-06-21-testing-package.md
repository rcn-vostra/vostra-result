# Vostra.Results.Testing Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the `Vostra.Results.Testing` package — a `TestHttpClient` that collapses an HTTP round-trip into a `Result<T>` (rebuilding typed errors from the response), plus zero-dependency fluent assertions, so integration tests read as domain scripts.

**Architecture:** A thin `TestHttpClient` issues HTTP verbs and delegates serialization to an injectable `IResultRawFormat` (default `RawJsonFormat`, System.Text.Json). Errors are reconstructed from the `Vostra.Results.AspNetCore` `problem+json` response by `ProblemDetailsErrorReader`. The client owns request context (verb/URL/body) and attaches it as error metadata; `VostraAssertionException` composes the rich diagnostic message only when an assertion fails. The package depends directly on `Vostra.Results.AspNetCore` to reuse its real envelope/`Pagination` types (zero drift).

**Tech Stack:** .NET 8/9, C# latest, System.Text.Json (in-box), xUnit + FluentAssertions 8.10 (test-only) + coverlet, Microsoft.AspNetCore.TestHost (round-trip tests).

**Design spec:** [docs/superpowers/specs/2026-06-21-testing-design.md](../specs/2026-06-21-testing-design.md)

## Global Constraints

- **Target frameworks:** `net8.0;net9.0` (parity with Core/AspNetCore; no `net10.0` this slice).
- **`Directory.Build.props` is inherited** and sets `Nullable=enable`, `ImplicitUsings=enable`, `LangVersion=latest`, **`TreatWarningsAsErrors=true`**, `EnforceCodeStyleInBuild=true`. All code must be warning-clean.
- **`GenerateDocumentationFile=true`** on the src project → **every public member needs an XML doc comment** or the build fails (warnings are errors). Test projects do not need XML docs.
- **Runtime dependency:** the src project references `Vostra.Results.AspNetCore` (which brings Core + the `Microsoft.AspNetCore.App` shared framework). No assertion library, no Newtonsoft in the src project.
- **Production assertions are hand-rolled zero-dep.** FluentAssertions may be used **only in the test project** (matching the existing test projects).
- **Commits must contain NO Claude/AI attribution** of any kind (no `Co-Authored-By` trailers, no "Generated with" lines). This is a hard repo rule.
- **No `InternalsVisibleTo`.** Use only the public surface of Core and AspNetCore.
- **Solution quirk:** the IDE occasionally strips project entries from `Vostra.Results.sln`. If `dotnet test`/`build` reports "Unable to find a project to restore", run `git restore Vostra.Results.sln` and re-add.

### Reference: Core/AspNetCore public API used by this plan (verbatim)

```csharp
// Vostra.Results (Core)
public enum ErrorType { Unexpected, Validation, NotFound, Conflict, Unauthorized, Forbidden }
public abstract class ErrorBase {
    public string Code { get; } public string Message { get; } public ErrorType Type { get; }
    public Exception? CausedBy { get; } public IReadOnlyDictionary<string, object?>? Metadata { get; }
    public ErrorBase WithMetadata(IReadOnlyDictionary<string, object?> metadata);  // preserves concrete type
}
public sealed class ValidationError : ErrorBase { public ValidationError(string message, string code = "General.Validation", Exception? causedBy = null, IReadOnlyDictionary<string,object?>? metadata = null); }
public sealed class NotFoundError : ErrorBase { public NotFoundError(string message, string code = "General.NotFound", ...); }
public sealed class ConflictError : ErrorBase { public ConflictError(string message, string code = "General.Conflict", ...); }
public sealed class UnauthorizedError : ErrorBase { public UnauthorizedError(string message, string code = "General.Unauthorized", ...); }
public sealed class ForbiddenError : ErrorBase { public ForbiddenError(string message, string code = "General.Forbidden", ...); }
public sealed class Error : ErrorBase { public Error(string message, string code = "General.Unexpected", ...); }   // ErrorType.Unexpected

public readonly partial struct Result<T> {
    public bool IsSuccess { get; } public bool IsError { get; } public SuccessKind SuccessKind { get; }
    public IReadOnlyList<ErrorBase> Errors { get; } public ErrorBase FirstError { get; }
    public bool TryGetValue(out T value); public bool TryGetErrors(out IReadOnlyList<ErrorBase>? errors);
    public static implicit operator Result<T>(T value);             // Ok success
    public static implicit operator Result<T>(ErrorBase error);     // failure
    public static implicit operator Result<T>(ErrorBase[] errors);  // failure
}
public readonly partial struct Result {
    public bool IsSuccess { get; } public bool IsError { get; } public SuccessKind SuccessKind { get; }
    public IReadOnlyList<ErrorBase> Errors { get; } public ErrorBase FirstError { get; }
    public static Result Success { get; } public static Result Created();
    public static Result<T> Created<T>(T value);                    // 201 success WITH value
    public static implicit operator Result(ErrorBase[] errors);    // failure
}
public enum SuccessKind { Ok, Created }

// Vostra.Results.AspNetCore  (reused types — DO NOT re-declare)
public sealed record Pagination(int Page, int PageSize, long TotalCount) { public int TotalPages { get; } }
public sealed class SuccessEnvelope<T> { public string? OperationId { get; init; } public T? Data { get; init; } }
public sealed class ListEnvelope<T> { public string? OperationId { get; init; } public IReadOnlyList<T> Data { get; init; } public Pagination Pagination { get; init; } }
// Error response is RFC 7807 problem+json with extensions: code, errorType, operationId, optional errors[]/errors{}.
// AddVostraResults() (IServiceCollection) and result.ToHttpResponse(HttpContext) → IResult are used only by the round-trip test host.
```

---

### Task 1: Scaffold projects, solution wiring, `PagedList<T>`

**Files:**
- Create: `src/Vostra.Results.Testing/Vostra.Results.Testing.csproj`
- Create: `src/Vostra.Results.Testing/PagedList.cs`
- Create: `tests/Vostra.Results.Testing.Tests/Vostra.Results.Testing.Tests.csproj`
- Create: `tests/Vostra.Results.Testing.Tests/GlobalUsings.cs`
- Create: `tests/Vostra.Results.Testing.Tests/PagedListTests.cs`
- Modify: `Vostra.Results.sln` (add both projects)

**Interfaces:**
- Produces: `Vostra.Results.Testing.PagedList<T>` — `public sealed record PagedList<T>(IReadOnlyList<T> Items, Pagination Pagination)` where `Pagination` is `Vostra.Results.AspNetCore.Pagination`.

- [ ] **Step 1: Create the src project file**

`src/Vostra.Results.Testing/Vostra.Results.Testing.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>net8.0;net9.0</TargetFrameworks>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <PackageId>Vostra.Results.Testing</PackageId>
    <Description>Integration-testing toolkit for Vostra.Results — a TestHttpClient returning Result&lt;T&gt; with typed-error reconstruction, plus fluent assertions.</Description>
    <PackageReadmeFile>README.md</PackageReadmeFile>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\Vostra.Results.AspNetCore\Vostra.Results.AspNetCore.csproj" />
  </ItemGroup>

  <ItemGroup>
    <None Include="README.md" Pack="true" PackagePath="\" Condition="Exists('README.md')" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Create `PagedList<T>`**

`src/Vostra.Results.Testing/PagedList.cs`:
```csharp
using Vostra.Results.AspNetCore;

namespace Vostra.Results.Testing;

/// <summary>A page of items plus its pagination metadata, returned by <c>TestHttpClient.GetList</c>.</summary>
/// <typeparam name="T">The element type.</typeparam>
/// <param name="Items">The items on this page.</param>
/// <param name="Pagination">The pagination metadata (reused from Vostra.Results.AspNetCore).</param>
public sealed record PagedList<T>(IReadOnlyList<T> Items, Pagination Pagination);
```

- [ ] **Step 3: Create the test project file**

`tests/Vostra.Results.Testing.Tests/Vostra.Results.Testing.Tests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>net8.0;net9.0</TargetFrameworks>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
    <PackageReference Include="coverlet.collector" Version="6.0.0" />
    <PackageReference Include="FluentAssertions" Version="8.10.0" />
    <PackageReference Include="Microsoft.AspNetCore.TestHost" Version="8.0.0" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="xunit" Version="2.5.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.3" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\Vostra.Results.Testing\Vostra.Results.Testing.csproj" />
    <ProjectReference Include="..\..\src\Vostra.Results.AspNetCore\Vostra.Results.AspNetCore.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 4: Create test global usings**

`tests/Vostra.Results.Testing.Tests/GlobalUsings.cs`:
```csharp
global using FluentAssertions;
global using Xunit;
global using Vostra.Results;
global using Vostra.Results.AspNetCore;
global using Vostra.Results.Testing;
```

- [ ] **Step 5: Write the smoke test for `PagedList<T>`**

`tests/Vostra.Results.Testing.Tests/PagedListTests.cs`:
```csharp
namespace Vostra.Results.Testing.Tests;

public class PagedListTests
{
    [Fact]
    public void PagedList_carries_items_and_pagination()
    {
        var page = new PagedList<string>(new[] { "a", "b" }, new Pagination(1, 20, 2));

        page.Items.Should().Equal("a", "b");
        page.Pagination.TotalCount.Should().Be(2);
    }
}
```

- [ ] **Step 6: Add both projects to the solution**

Run:
```bash
dotnet sln Vostra.Results.sln add src/Vostra.Results.Testing/Vostra.Results.Testing.csproj tests/Vostra.Results.Testing.Tests/Vostra.Results.Testing.Tests.csproj
```
Expected: "Project ... added to the solution." twice.

- [ ] **Step 7: Build and run the test**

Run:
```bash
dotnet test tests/Vostra.Results.Testing.Tests/Vostra.Results.Testing.Tests.csproj
```
Expected: build succeeds (warnings-as-errors clean), 1 test passes.

- [ ] **Step 8: Commit**

```bash
git add src/Vostra.Results.Testing tests/Vostra.Results.Testing.Tests Vostra.Results.sln
git commit -m "feat(testing): scaffold Vostra.Results.Testing project, test project, PagedList<T>"
```

---

### Task 2: `ProblemDetailsErrorReader` — reconstruct typed errors

**Files:**
- Create: `src/Vostra.Results.Testing/ProblemDetailsErrorReader.cs`
- Create: `tests/Vostra.Results.Testing.Tests/ProblemDetailsErrorReaderTests.cs`

**Interfaces:**
- Produces: `internal static class ProblemDetailsErrorReader` with `public static IReadOnlyList<ErrorBase> Read(JsonElement root, int statusCode)`. Maps `errorType` → concrete `ErrorBase`; branches on validation map vs error array vs single error. (`internal` is fine — the test project is in the same assembly? No — it's a separate assembly. Make it `public` so tests can call it directly without `InternalsVisibleTo`.)

> Note: this type is `public` so tests can exercise it directly (we forbid `InternalsVisibleTo`). It is low-level but harmless to expose.

- [ ] **Step 1: Write the failing tests**

`tests/Vostra.Results.Testing.Tests/ProblemDetailsErrorReaderTests.cs`:
```csharp
using System.Text.Json;

namespace Vostra.Results.Testing.Tests;

public class ProblemDetailsErrorReaderTests
{
    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void Single_error_rebuilds_typed_error_with_code_and_message()
    {
        var json = """
        { "status":404, "title":"Not Found", "detail":"Order 7 not found",
          "code":"Order.NotFound", "errorType":"NotFound" }
        """;

        var errors = ProblemDetailsErrorReader.Read(Parse(json), 404);

        errors.Should().HaveCount(1);
        errors[0].Should().BeOfType<NotFoundError>();
        errors[0].Code.Should().Be("Order.NotFound");
        errors[0].Message.Should().Be("Order 7 not found");
        errors[0].Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public void Validation_map_rebuilds_one_ValidationError_per_field_message_with_field_metadata()
    {
        var json = """
        { "status":400, "title":"Bad Request",
          "errors": { "name":["Required."], "age":["Must be >= 0.","Too large."] },
          "code":"General.Validation", "errorType":"Validation" }
        """;

        var errors = ProblemDetailsErrorReader.Read(Parse(json), 400);

        errors.Should().HaveCount(3);
        errors.Should().AllBeOfType<ValidationError>();
        errors.Select(e => (string?)e.Metadata!["field"]).Should().Equal("name", "age", "age");
        errors.Select(e => e.Message).Should().Equal("Required.", "Must be >= 0.", "Too large.");
        errors.Should().OnlyContain(e => e.Code == "General.Validation");
    }

    [Fact]
    public void Multi_error_array_rebuilds_one_typed_error_per_entry()
    {
        var json = """
        { "status":409, "code":"A.X", "errorType":"Conflict",
          "errors":[ {"code":"A.X","message":"x clash"}, {"code":"A.Y","message":"y clash"} ] }
        """;

        var errors = ProblemDetailsErrorReader.Read(Parse(json), 409);

        errors.Should().HaveCount(2);
        errors.Should().AllBeOfType<ConflictError>();
        errors.Select(e => e.Code).Should().Equal("A.X", "A.Y");
        errors.Select(e => e.Message).Should().Equal("x clash", "y clash");
    }

    [Theory]
    [InlineData("Unauthorized", typeof(UnauthorizedError), ErrorType.Unauthorized)]
    [InlineData("Forbidden", typeof(ForbiddenError), ErrorType.Forbidden)]
    [InlineData("Unexpected", typeof(Error), ErrorType.Unexpected)]
    public void ErrorType_maps_to_concrete_kind(string errorType, Type clrType, ErrorType expected)
    {
        var json = $$"""{ "status":400, "detail":"d", "code":"C", "errorType":"{{errorType}}" }""";

        var errors = ProblemDetailsErrorReader.Read(Parse(json), 400);

        errors[0].Should().BeOfType(clrType);
        errors[0].Type.Should().Be(expected);
    }

    [Fact]
    public void Unknown_errorType_falls_back_to_unexpected_Error()
    {
        var json = """{ "status":418, "detail":"teapot", "code":"X", "errorType":"Teapot" }""";

        var errors = ProblemDetailsErrorReader.Read(Parse(json), 418);

        errors[0].Should().BeOfType<Error>();
        errors[0].Type.Should().Be(ErrorType.Unexpected);
    }

    [Fact]
    public void Missing_code_defaults_and_uses_title_when_no_detail()
    {
        var json = """{ "status":500, "title":"Internal Server Error" }""";

        var errors = ProblemDetailsErrorReader.Read(Parse(json), 500);

        errors[0].Should().BeOfType<Error>();
        errors[0].Code.Should().Be("General.Unexpected");
        errors[0].Message.Should().Be("Internal Server Error");
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run:
```bash
dotnet test tests/Vostra.Results.Testing.Tests --filter ProblemDetailsErrorReaderTests
```
Expected: FAIL — `ProblemDetailsErrorReader` does not exist (compile error).

- [ ] **Step 3: Implement `ProblemDetailsErrorReader`**

`src/Vostra.Results.Testing/ProblemDetailsErrorReader.cs`:
```csharp
using System.Text.Json;
using Vostra.Results;

namespace Vostra.Results.Testing;

/// <summary>
/// Reconstructs typed <see cref="ErrorBase"/> instances from an RFC 7807 <c>problem+json</c>
/// document produced by Vostra.Results.AspNetCore. Branches on the <c>errorType</c> extension:
/// validation responses carry a field→messages object map; multi-error responses carry an
/// <c>errors</c> array; single errors carry identity only in the top-level extensions.
/// </summary>
public static class ProblemDetailsErrorReader
{
    /// <summary>Reads the problem document into one or more typed errors.</summary>
    /// <param name="root">The parsed problem-details JSON object.</param>
    /// <param name="statusCode">The HTTP status code of the response.</param>
    public static IReadOnlyList<ErrorBase> Read(JsonElement root, int statusCode)
    {
        var errorTypeText = GetString(root, "errorType");
        var type = ParseType(errorTypeText);
        var code = GetString(root, "code") ?? "General.Unexpected";
        var detail = GetString(root, "detail") ?? GetString(root, "title") ?? $"HTTP {statusCode}";

        if (type == ErrorType.Validation
            && root.TryGetProperty("errors", out var map)
            && map.ValueKind == JsonValueKind.Object)
        {
            var validation = new List<ErrorBase>();
            foreach (var field in map.EnumerateObject())
            {
                if (field.Value.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var message in field.Value.EnumerateArray())
                {
                    var metadata = new Dictionary<string, object?> { ["field"] = field.Name };
                    validation.Add(new ValidationError(message.GetString() ?? string.Empty, code, metadata: metadata));
                }
            }

            if (validation.Count > 0)
            {
                return validation;
            }
        }

        if (root.TryGetProperty("errors", out var array) && array.ValueKind == JsonValueKind.Array)
        {
            var many = new List<ErrorBase>();
            foreach (var entry in array.EnumerateArray())
            {
                var entryCode = GetString(entry, "code") ?? code;
                var entryMessage = GetString(entry, "message") ?? detail;
                many.Add(Create(type, entryCode, entryMessage));
            }

            if (many.Count > 0)
            {
                return many;
            }
        }

        return new[] { Create(type, code, detail) };
    }

    private static ErrorBase Create(ErrorType type, string code, string message) => type switch
    {
        ErrorType.Validation => new ValidationError(message, code),
        ErrorType.NotFound => new NotFoundError(message, code),
        ErrorType.Conflict => new ConflictError(message, code),
        ErrorType.Unauthorized => new UnauthorizedError(message, code),
        ErrorType.Forbidden => new ForbiddenError(message, code),
        _ => new Error(message, code),
    };

    private static ErrorType ParseType(string? text) =>
        Enum.TryParse<ErrorType>(text, ignoreCase: true, out var parsed) ? parsed : ErrorType.Unexpected;

    private static string? GetString(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(property, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run:
```bash
dotnet test tests/Vostra.Results.Testing.Tests --filter ProblemDetailsErrorReaderTests
```
Expected: PASS (all reader tests green).

- [ ] **Step 5: Commit**

```bash
git add src/Vostra.Results.Testing/ProblemDetailsErrorReader.cs tests/Vostra.Results.Testing.Tests/ProblemDetailsErrorReaderTests.cs
git commit -m "feat(testing): add ProblemDetailsErrorReader rebuilding typed errors from problem+json"
```

---

### Task 3: `IResultRawFormat` + `RawJsonFormat`

**Files:**
- Create: `src/Vostra.Results.Testing/IResultRawFormat.cs`
- Create: `src/Vostra.Results.Testing/RawJsonFormat.cs`
- Create: `tests/Vostra.Results.Testing.Tests/RawJsonFormatTests.cs`

**Interfaces:**
- Consumes: `ProblemDetailsErrorReader.Read(JsonElement, int)`, `PagedList<T>`, AspNetCore `Pagination`.
- Produces:
  - `public interface IResultRawFormat` with:
    - `HttpContent SerializeRequest(object body)`
    - `Task<T?> ReadData<T>(HttpResponseMessage response, CancellationToken ct)`
    - `Task<PagedList<T>?> ReadList<T>(HttpResponseMessage response, CancellationToken ct)`
    - `Task<IReadOnlyList<ErrorBase>> ReadErrors(HttpResponseMessage response, CancellationToken ct)`
  - `public sealed class RawJsonFormat : IResultRawFormat` with `public static RawJsonFormat Instance { get; }`.

> Refinement vs spec §5: a dedicated `ReadList<T>` is added (the spec folded lists into `ReadData`); pagination needs its own return shape. Strict deserialization (`UnmappedMemberHandling.Disallow`) is scoped to the payload `T` only — the envelope is parsed via `JsonDocument` (tolerant), then `data` is deserialized into `T` with strict options.

- [ ] **Step 1: Write the failing tests**

`tests/Vostra.Results.Testing.Tests/RawJsonFormatTests.cs`:
```csharp
using System.Net;
using System.Net.Http;
using System.Text;

namespace Vostra.Results.Testing.Tests;

public class RawJsonFormatTests
{
    private sealed record Product(int Id, string Name);

    private static HttpResponseMessage Json(HttpStatusCode status, string body, string contentType = "application/json") =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, contentType) };

    [Fact]
    public async Task ReadData_unwraps_the_success_envelope_data()
    {
        var resp = Json(HttpStatusCode.OK, """{ "operationId":"op-1", "data": { "id":7, "name":"Widget" } }""");

        var data = await RawJsonFormat.Instance.ReadData<Product>(resp, default);

        data.Should().Be(new Product(7, "Widget"));
    }

    [Fact]
    public async Task ReadData_throws_helpful_hint_on_unmapped_member()
    {
        var resp = Json(HttpStatusCode.OK, """{ "operationId":"op-1", "data": { "id":7, "name":"Widget", "extra":1 } }""");

        var act = async () => await RawJsonFormat.Instance.ReadData<Product>(resp, default);

        (await act.Should().ThrowAsync<System.Text.Json.JsonException>())
            .WithMessage("*MAKE SURE YOU HAVE MAPPED TO THE CORRECT RESPONSE OBJECT*");
    }

    [Fact]
    public async Task ReadList_unwraps_items_and_pagination()
    {
        var resp = Json(HttpStatusCode.OK, """
        { "operationId":"op-1",
          "data":[ {"id":1,"name":"a"}, {"id":2,"name":"b"} ],
          "pagination": { "page":1, "pageSize":20, "totalCount":2, "totalPages":1 } }
        """);

        var page = await RawJsonFormat.Instance.ReadList<Product>(resp, default);

        page!.Items.Should().Equal(new Product(1, "a"), new Product(2, "b"));
        page.Pagination.TotalCount.Should().Be(2);
        page.Pagination.Page.Should().Be(1);
    }

    [Fact]
    public async Task ReadErrors_uses_ProblemDetailsErrorReader_for_problem_json()
    {
        var resp = Json(HttpStatusCode.NotFound,
            """{ "status":404, "detail":"gone", "code":"X.NotFound", "errorType":"NotFound" }""",
            "application/problem+json");

        var errors = await RawJsonFormat.Instance.ReadErrors(resp, default);

        errors.Should().ContainSingle().Which.Code.Should().Be("X.NotFound");
        errors[0].Should().BeOfType<NotFoundError>();
    }

    [Fact]
    public async Task ReadErrors_wraps_non_json_body_as_unexpected_error()
    {
        var resp = Json(HttpStatusCode.InternalServerError, "boom (stack trace)", "text/plain");

        var errors = await RawJsonFormat.Instance.ReadErrors(resp, default);

        errors.Should().ContainSingle();
        errors[0].Should().BeOfType<Error>();
        errors[0].Message.Should().Be("boom (stack trace)");
    }

    [Fact]
    public async Task ReadErrors_wraps_empty_body_with_status_message()
    {
        var resp = new HttpResponseMessage(HttpStatusCode.BadGateway) { Content = new StringContent(string.Empty) };

        var errors = await RawJsonFormat.Instance.ReadErrors(resp, default);

        errors.Should().ContainSingle();
        errors[0].Should().BeOfType<Error>();
        errors[0].Message.Should().Contain("502");
    }

    [Fact]
    public void SerializeRequest_produces_json_content()
    {
        var content = RawJsonFormat.Instance.SerializeRequest(new Product(1, "a"));

        content.Headers.ContentType!.MediaType.Should().Be("application/json");
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run:
```bash
dotnet test tests/Vostra.Results.Testing.Tests --filter RawJsonFormatTests
```
Expected: FAIL — `IResultRawFormat`/`RawJsonFormat` do not exist.

- [ ] **Step 3: Create the interface**

`src/Vostra.Results.Testing/IResultRawFormat.cs`:
```csharp
using Vostra.Results;

namespace Vostra.Results.Testing;

/// <summary>
/// Translates between HTTP request/response payloads and <see cref="Result{T}"/> building blocks.
/// This is the injection seam (serializer + envelope shape). The default is <see cref="RawJsonFormat"/>.
/// </summary>
public interface IResultRawFormat
{
    /// <summary>Serializes a request body to <see cref="HttpContent"/>.</summary>
    HttpContent SerializeRequest(object body);

    /// <summary>Deserializes the success envelope's <c>data</c> payload into <typeparamref name="T"/>.</summary>
    Task<T?> ReadData<T>(HttpResponseMessage response, CancellationToken ct);

    /// <summary>Deserializes a list success envelope into items plus pagination.</summary>
    Task<PagedList<T>?> ReadList<T>(HttpResponseMessage response, CancellationToken ct);

    /// <summary>Reconstructs the typed error(s) from an error response.</summary>
    Task<IReadOnlyList<ErrorBase>> ReadErrors(HttpResponseMessage response, CancellationToken ct);
}
```

- [ ] **Step 4: Implement `RawJsonFormat`**

`src/Vostra.Results.Testing/RawJsonFormat.cs`:
```csharp
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Vostra.Results;
using Vostra.Results.AspNetCore;

namespace Vostra.Results.Testing;

/// <summary>
/// Default <see cref="IResultRawFormat"/> backed by System.Text.Json. Reads the Vostra.Results.AspNetCore
/// success envelopes and reconstructs typed errors from <c>problem+json</c>. Payload deserialization is
/// strict (unmapped members throw a helpful hint); the envelope itself is parsed tolerantly.
/// </summary>
public sealed class RawJsonFormat : IResultRawFormat
{
    private const string MappingHint =
        " — MAKE SURE YOU HAVE MAPPED TO THE CORRECT RESPONSE OBJECT (THAT CONTROLLER RESPONDS WITH)";

    private static readonly JsonSerializerOptions Tolerant = new(JsonSerializerDefaults.Web);

    private static readonly JsonSerializerOptions Strict = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    /// <summary>A shared, stateless default instance.</summary>
    public static RawJsonFormat Instance { get; } = new();

    /// <inheritdoc />
    public HttpContent SerializeRequest(object body) =>
        new StringContent(JsonSerializer.Serialize(body, Tolerant), Encoding.UTF8, "application/json");

    /// <inheritdoc />
    public async Task<T?> ReadData<T>(HttpResponseMessage response, CancellationToken ct)
    {
        var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        using var document = JsonDocument.Parse(json);
        return document.RootElement.TryGetProperty("data", out var data)
            ? Deserialize<T>(data)
            : default;
    }

    /// <inheritdoc />
    public async Task<PagedList<T>?> ReadList<T>(HttpResponseMessage response, CancellationToken ct)
    {
        var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var items = new List<T>();
        if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in data.EnumerateArray())
            {
                var item = Deserialize<T>(element);
                if (item is not null)
                {
                    items.Add(item);
                }
            }
        }

        var pagination = root.TryGetProperty("pagination", out var p)
            ? p.Deserialize<Pagination>(Tolerant) ?? new Pagination(1, 0, 0)
            : new Pagination(1, 0, 0);

        return new PagedList<T>(items, pagination);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ErrorBase>> ReadErrors(HttpResponseMessage response, CancellationToken ct)
    {
        var status = (int)response.StatusCode;
        var body = (await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false)).Trim();

        if (body.Length == 0)
        {
            return new ErrorBase[] { new Error($"HTTP {status} {response.ReasonPhrase}".Trim(), "Http.Error") };
        }

        if (body[0] != '{' && body[0] != '[')
        {
            return new ErrorBase[] { new Error(body, "Http.Error") };
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            return ProblemDetailsErrorReader.Read(document.RootElement, status);
        }
        catch (JsonException)
        {
            return new ErrorBase[] { new Error(body, "Http.Error") };
        }
    }

    private static T? Deserialize<T>(JsonElement element)
    {
        try
        {
            return element.Deserialize<T>(Strict);
        }
        catch (JsonException ex) when (ex.Message.Contains("could not be mapped", StringComparison.OrdinalIgnoreCase))
        {
            throw new JsonException(ex.Message + MappingHint, ex);
        }
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run:
```bash
dotnet test tests/Vostra.Results.Testing.Tests --filter RawJsonFormatTests
```
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/Vostra.Results.Testing/IResultRawFormat.cs src/Vostra.Results.Testing/RawJsonFormat.cs tests/Vostra.Results.Testing.Tests/RawJsonFormatTests.cs
git commit -m "feat(testing): add IResultRawFormat seam and RawJsonFormat (STJ) reader"
```

---

### Task 4: `TestHttpClient` (all verbs + request context)

**Files:**
- Create: `src/Vostra.Results.Testing/RequestContext.cs`
- Create: `src/Vostra.Results.Testing/TestHttpClient.cs`
- Create: `tests/Vostra.Results.Testing.Tests/StubHttpMessageHandler.cs`
- Create: `tests/Vostra.Results.Testing.Tests/TestHttpClientTests.cs`

**Interfaces:**
- Consumes: `IResultRawFormat`, `RawJsonFormat.Instance`, `PagedList<T>`, Core `Result`/`Result<T>`/`Result.Created<T>`.
- Produces:
  - `public sealed record RequestContext(string Verb, string Url, object? Body)` — attached to a failed error's `Metadata["request"]`.
  - `public sealed class TestHttpClient` with constructor `(HttpClient client, string baseUrl = "", IResultRawFormat? format = null)` and methods:
    `Task<Result<T>> Get<T>(string url = "", CancellationToken ct = default)`,
    `Task<Result<PagedList<T>>> GetList<T>(string url = "", CancellationToken ct = default)`,
    `Task<Result<T>> Post<T>(string url, object body, CancellationToken ct = default)`, `Task<Result> Post(string url, object body, CancellationToken ct = default)`,
    `Task<Result<T>> Put<T>(...)`, `Task<Result> Put(...)`,
    `Task<Result<T>> Patch<T>(...)`, `Task<Result> Patch(...)`,
    `Task<Result> Delete(string url, CancellationToken ct = default)`, `Task<Result<T>> Delete<T>(string url, CancellationToken ct = default)`.

- [ ] **Step 1: Write the stub handler test helper**

`tests/Vostra.Results.Testing.Tests/StubHttpMessageHandler.cs`:
```csharp
using System.Net;
using System.Net.Http;
using System.Text;

namespace Vostra.Results.Testing.Tests;

/// <summary>Returns a canned response and records the last request for assertions.</summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _status;
    private readonly string _body;
    private readonly string _contentType;

    public StubHttpMessageHandler(HttpStatusCode status, string body, string contentType = "application/json")
    {
        _status = status;
        _body = body;
        _contentType = contentType;
    }

    public HttpRequestMessage? LastRequest { get; private set; }
    public string? LastRequestBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        LastRequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        return new HttpResponseMessage(_status)
        {
            Content = new StringContent(_body, Encoding.UTF8, _contentType),
        };
    }

    public static HttpClient Client(HttpStatusCode status, string body, string contentType = "application/json") =>
        new(new StubHttpMessageHandler(status, body, contentType)) { BaseAddress = new Uri("http://localhost/") };
}
```

- [ ] **Step 2: Write the failing client tests**

`tests/Vostra.Results.Testing.Tests/TestHttpClientTests.cs`:
```csharp
using System.Net;

namespace Vostra.Results.Testing.Tests;

public class TestHttpClientTests
{
    private sealed record Product(int Id, string Name);

    [Fact]
    public async Task Get_success_returns_value_result()
    {
        var http = StubHttpMessageHandler.Client(HttpStatusCode.OK, """{ "data": { "id":7, "name":"Widget" } }""");
        var api = new TestHttpClient(http, "products");

        var result = await api.Get<Product>("/7");

        result.IsSuccess.Should().BeTrue();
        result.TryGetValue(out var value).Should().BeTrue();
        value.Should().Be(new Product(7, "Widget"));
    }

    [Fact]
    public async Task Created_status_yields_SuccessKind_Created()
    {
        var http = StubHttpMessageHandler.Client(HttpStatusCode.Created, """{ "data": { "id":7, "name":"Widget" } }""");
        var api = new TestHttpClient(http, "products");

        var result = await api.Post<Product>("", new Product(0, "Widget"));

        result.IsSuccess.Should().BeTrue();
        result.SuccessKind.Should().Be(SuccessKind.Created);
    }

    [Fact]
    public async Task Error_status_returns_typed_error_result()
    {
        var http = StubHttpMessageHandler.Client(HttpStatusCode.NotFound,
            """{ "status":404, "detail":"gone", "code":"Product.NotFound", "errorType":"NotFound" }""",
            "application/problem+json");
        var api = new TestHttpClient(http, "products");

        var result = await api.Get<Product>("/9");

        result.IsError.Should().BeTrue();
        result.FirstError.Should().BeOfType<NotFoundError>();
        result.FirstError.Code.Should().Be("Product.NotFound");
    }

    [Fact]
    public async Task Failed_request_attaches_request_context_metadata()
    {
        var http = StubHttpMessageHandler.Client(HttpStatusCode.BadRequest,
            """{ "status":400, "detail":"bad", "code":"X", "errorType":"Validation" }""",
            "application/problem+json");
        var api = new TestHttpClient(http, "products");

        var result = await api.Post<Product>("/x", new Product(1, "a"));

        result.FirstError.Metadata.Should().ContainKey("request");
        var ctx = (RequestContext)result.FirstError.Metadata!["request"]!;
        ctx.Verb.Should().Be("POST");
        ctx.Url.Should().Be("products/x");
        ctx.Body.Should().BeEquivalentTo(new Product(1, "a"));
    }

    [Fact]
    public async Task Delete_nongeneric_success_returns_ok_result()
    {
        var http = StubHttpMessageHandler.Client(HttpStatusCode.OK, """{ "operationId":"op-1" }""");
        var api = new TestHttpClient(http, "products");

        var result = await api.Delete("/7");

        result.IsSuccess.Should().BeTrue();
        result.SuccessKind.Should().Be(SuccessKind.Ok);
    }

    [Fact]
    public async Task Get_baseUrl_and_url_are_joined_with_single_slash()
    {
        var stub = new StubHttpMessageHandler(HttpStatusCode.OK, """{ "data": { "id":1, "name":"a" } }""");
        var http = new HttpClient(stub) { BaseAddress = new Uri("http://localhost/") };
        var api = new TestHttpClient(http, "products/");

        await api.Get<Product>("/7");

        stub.LastRequest!.RequestUri!.AbsolutePath.Should().Be("/products/7");
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run:
```bash
dotnet test tests/Vostra.Results.Testing.Tests --filter TestHttpClientTests
```
Expected: FAIL — `TestHttpClient`/`RequestContext` do not exist.

- [ ] **Step 4: Create `RequestContext`**

`src/Vostra.Results.Testing/RequestContext.cs`:
```csharp
namespace Vostra.Results.Testing;

/// <summary>The request that produced a failed result, attached to the failing error's metadata
/// under the key <c>"request"</c> and surfaced in assertion failure messages.</summary>
/// <param name="Verb">The HTTP verb (e.g. "GET").</param>
/// <param name="Url">The resolved request URL.</param>
/// <param name="Body">The request body, if any.</param>
public sealed record RequestContext(string Verb, string Url, object? Body);
```

- [ ] **Step 5: Implement `TestHttpClient`**

`src/Vostra.Results.Testing/TestHttpClient.cs`:
```csharp
using System.Net;
using System.Net.Http;
using Vostra.Results;

namespace Vostra.Results.Testing;

/// <summary>
/// An async test client that issues HTTP verbs against an API built with Vostra.Results.AspNetCore and
/// collapses each response into a <see cref="Result{T}"/>, reconstructing typed errors on failure.
/// </summary>
public sealed class TestHttpClient
{
    private readonly HttpClient _client;
    private readonly string _baseUrl;
    private readonly IResultRawFormat _format;

    /// <summary>Creates a client over an existing <see cref="HttpClient"/>.</summary>
    /// <param name="client">The underlying client (e.g. from a test server).</param>
    /// <param name="baseUrl">A base path prepended to every request URL.</param>
    /// <param name="format">The wire format; defaults to <see cref="RawJsonFormat.Instance"/>.</param>
    public TestHttpClient(HttpClient client, string baseUrl = "", IResultRawFormat? format = null)
    {
        _client = client;
        _baseUrl = baseUrl;
        _format = format ?? RawJsonFormat.Instance;
    }

    /// <summary>Issues GET and returns the deserialized value.</summary>
    public Task<Result<T>> Get<T>(string url = "", CancellationToken ct = default) =>
        SendData<T>(HttpMethod.Get, url, body: null, ct);

    /// <summary>Issues GET and returns a page of items plus pagination.</summary>
    public async Task<Result<PagedList<T>>> GetList<T>(string url = "", CancellationToken ct = default)
    {
        using var request = BuildRequest(HttpMethod.Get, url, body: null);
        using var response = await _client.SendAsync(request, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return Fail(await _format.ReadErrors(response, ct).ConfigureAwait(false), HttpMethod.Get, url, body: null);
        }

        var page = await _format.ReadList<T>(response, ct).ConfigureAwait(false);
        return page is null
            ? (ErrorBase)new Error($"GET {Combine(url)} returned no list payload", "Http.NullResponse")
            : page;
    }

    /// <summary>Issues POST and returns the deserialized value.</summary>
    public Task<Result<T>> Post<T>(string url, object body, CancellationToken ct = default) =>
        SendData<T>(HttpMethod.Post, url, body, ct);

    /// <summary>Issues POST and returns a valueless result.</summary>
    public Task<Result> Post(string url, object body, CancellationToken ct = default) =>
        SendNoData(HttpMethod.Post, url, body, ct);

    /// <summary>Issues PUT and returns the deserialized value.</summary>
    public Task<Result<T>> Put<T>(string url, object body, CancellationToken ct = default) =>
        SendData<T>(HttpMethod.Put, url, body, ct);

    /// <summary>Issues PUT and returns a valueless result.</summary>
    public Task<Result> Put(string url, object body, CancellationToken ct = default) =>
        SendNoData(HttpMethod.Put, url, body, ct);

    /// <summary>Issues PATCH and returns the deserialized value.</summary>
    public Task<Result<T>> Patch<T>(string url, object body, CancellationToken ct = default) =>
        SendData<T>(HttpMethod.Patch, url, body, ct);

    /// <summary>Issues PATCH and returns a valueless result.</summary>
    public Task<Result> Patch(string url, object body, CancellationToken ct = default) =>
        SendNoData(HttpMethod.Patch, url, body, ct);

    /// <summary>Issues DELETE and returns a valueless result.</summary>
    public Task<Result> Delete(string url, CancellationToken ct = default) =>
        SendNoData(HttpMethod.Delete, url, body: null, ct);

    /// <summary>Issues DELETE and returns the deserialized value.</summary>
    public Task<Result<T>> Delete<T>(string url, CancellationToken ct = default) =>
        SendData<T>(HttpMethod.Delete, url, body: null, ct);

    private async Task<Result<T>> SendData<T>(HttpMethod method, string url, object? body, CancellationToken ct)
    {
        using var request = BuildRequest(method, url, body);
        using var response = await _client.SendAsync(request, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return Fail(await _format.ReadErrors(response, ct).ConfigureAwait(false), method, url, body);
        }

        var data = await _format.ReadData<T>(response, ct).ConfigureAwait(false);
        if (data is null)
        {
            return (ErrorBase)new Error($"{method.Method} {Combine(url)} returned null data", "Http.NullResponse");
        }

        return response.StatusCode == HttpStatusCode.Created ? Result.Created(data) : data;
    }

    private async Task<Result> SendNoData(HttpMethod method, string url, object? body, CancellationToken ct)
    {
        using var request = BuildRequest(method, url, body);
        using var response = await _client.SendAsync(request, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var errors = await _format.ReadErrors(response, ct).ConfigureAwait(false);
            return AttachRequest(errors, method, url, body);
        }

        return response.StatusCode == HttpStatusCode.Created ? Result.Created() : Result.Success;
    }

    private Result<T> Fail<T>(IReadOnlyList<ErrorBase> errors, HttpMethod method, string url, object? body) =>
        AttachRequest(errors, method, url, body);

    private ErrorBase[] AttachRequest(IReadOnlyList<ErrorBase> errors, HttpMethod method, string url, object? body)
    {
        var array = errors.ToArray();
        if (array.Length == 0)
        {
            return new ErrorBase[] { new Error($"{method.Method} {Combine(url)} failed", "Http.Error") };
        }

        var metadata = new Dictionary<string, object?>();
        if (array[0].Metadata is { } existing)
        {
            foreach (var pair in existing)
            {
                metadata[pair.Key] = pair.Value;
            }
        }

        metadata["request"] = new RequestContext(method.Method, Combine(url), body);
        array[0] = array[0].WithMetadata(metadata);
        return array;
    }

    private HttpRequestMessage BuildRequest(HttpMethod method, string url, object? body)
    {
        var request = new HttpRequestMessage(method, Combine(url));
        if (body is not null)
        {
            request.Content = _format.SerializeRequest(body);
        }

        return request;
    }

    private string Combine(string url)
    {
        if (string.IsNullOrEmpty(_baseUrl))
        {
            return url.TrimStart('/');
        }

        var left = _baseUrl.TrimEnd('/');
        var right = url.TrimStart('/');
        return right.Length == 0 ? left : $"{left}/{right}";
    }
}
```

- [ ] **Step 6: Run the tests to verify they pass**

Run:
```bash
dotnet test tests/Vostra.Results.Testing.Tests --filter TestHttpClientTests
```
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add src/Vostra.Results.Testing/RequestContext.cs src/Vostra.Results.Testing/TestHttpClient.cs tests/Vostra.Results.Testing.Tests/StubHttpMessageHandler.cs tests/Vostra.Results.Testing.Tests/TestHttpClientTests.cs
git commit -m "feat(testing): add TestHttpClient verbs with typed-error and request-context handling"
```

---

### Task 5: `VostraAssertionException` + synchronous assertions

**Files:**
- Create: `src/Vostra.Results.Testing/VostraAssertionException.cs`
- Create: `src/Vostra.Results.Testing/ResultAssertions.cs`
- Create: `tests/Vostra.Results.Testing.Tests/ResultAssertionsTests.cs`

**Interfaces:**
- Consumes: Core `Result<T>`/`Result`/`ErrorBase`/`ErrorType`, `RequestContext`.
- Produces:
  - `public sealed class VostraAssertionException : Exception` (prefixed to avoid clashing with NUnit's `AssertionException`).
  - `public static class ResultAssertions` (extension methods on `Result<T>` and `Result`):
    - `T ShouldBeSuccess<T>(this Result<T> result)` — terminal, returns the value.
    - `Result ShouldBeSuccess(this Result result)` — chainable (no value).
    - `Result<T> ShouldHaveError<T>(this Result<T> result, string code)` / `Result ShouldHaveError(this Result result, string code)`
    - `Result<T> ShouldHaveError<T>(this Result<T> result, ErrorType type)` / `Result ShouldHaveError(this Result result, ErrorType type)`
    - `Result<T> Assert<T>(this Result<T> result, Action<T> assertion)` — implies success, runs the action, returns the result.
    - kind sugar: `ShouldBeNotFound`, `ShouldBeValidation`, `ShouldBeConflict`, `ShouldBeUnauthorized`, `ShouldBeForbidden`, `ShouldBeUnexpected` (on both `Result<T>` and `Result`, each delegating to `ShouldHaveError(ErrorType)`).
  - `internal static string Describe(IReadOnlyList<ErrorBase> errors)` — composes the rich S6 message from `Metadata["request"]`.

- [ ] **Step 1: Write the failing tests**

`tests/Vostra.Results.Testing.Tests/ResultAssertionsTests.cs`:
```csharp
namespace Vostra.Results.Testing.Tests;

public class ResultAssertionsTests
{
    [Fact]
    public void ShouldBeSuccess_returns_value_on_success()
    {
        Result<int> result = 42;

        var value = result.ShouldBeSuccess();

        value.Should().Be(42);
    }

    [Fact]
    public void ShouldBeSuccess_throws_on_failure()
    {
        Result<int> result = new NotFoundError("nope", "X.NotFound");

        var act = () => result.ShouldBeSuccess();

        act.Should().Throw<VostraAssertionException>().WithMessage("*X.NotFound*");
    }

    [Fact]
    public void ShouldHaveError_by_code_passes_when_any_error_matches()
    {
        Result<int> result = new ErrorBase[] { new ValidationError("a", "F.A"), new ValidationError("b", "F.B") };

        var chained = result.ShouldHaveError("F.B");

        chained.Should().Be(result);
    }

    [Fact]
    public void ShouldHaveError_by_code_throws_when_no_error_matches()
    {
        Result<int> result = new NotFoundError("nope", "X.NotFound");

        var act = () => result.ShouldHaveError("Other.Code");

        act.Should().Throw<VostraAssertionException>();
    }

    [Fact]
    public void ShouldHaveError_by_type_passes()
    {
        Result<int> result = new NotFoundError("nope", "X.NotFound");

        result.ShouldHaveError(ErrorType.NotFound);
    }

    [Fact]
    public void ShouldBeNotFound_sugar_passes()
    {
        Result<int> result = new NotFoundError("nope", "X.NotFound");

        result.ShouldBeNotFound();
    }

    [Fact]
    public void Assert_runs_action_on_success_and_returns_result()
    {
        Result<int> result = 10;
        var seen = 0;

        var chained = result.Assert(v => seen = v);

        seen.Should().Be(10);
        chained.Should().Be(result);
    }

    [Fact]
    public void Assert_throws_when_result_is_failure()
    {
        Result<int> result = new NotFoundError("nope", "X.NotFound");

        var act = () => result.Assert(_ => { });

        act.Should().Throw<VostraAssertionException>();
    }

    [Fact]
    public void Failure_message_includes_request_context_when_present()
    {
        var error = new NotFoundError("gone", "X.NotFound")
            .WithMetadata(new Dictionary<string, object?> { ["request"] = new RequestContext("GET", "products/9", null) });
        Result<int> result = (ErrorBase)error;

        var act = () => result.ShouldBeSuccess();

        act.Should().Throw<VostraAssertionException>()
            .WithMessage("*GET*products/9*");
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run:
```bash
dotnet test tests/Vostra.Results.Testing.Tests --filter ResultAssertionsTests
```
Expected: FAIL — `VostraAssertionException`/`ResultAssertions` do not exist.

- [ ] **Step 3: Create the exception**

`src/Vostra.Results.Testing/VostraAssertionException.cs`:
```csharp
namespace Vostra.Results.Testing;

/// <summary>Thrown when a Vostra result assertion fails. Prefixed to avoid an ambiguous-reference
/// clash with NUnit's <c>AssertionException</c>.</summary>
public sealed class VostraAssertionException : Exception
{
    /// <summary>Creates the exception with a diagnostic message.</summary>
    public VostraAssertionException(string message) : base(message) { }
}
```

- [ ] **Step 4: Implement `ResultAssertions`**

`src/Vostra.Results.Testing/ResultAssertions.cs`:
```csharp
using System.Text;
using Vostra.Results;

namespace Vostra.Results.Testing;

/// <summary>Fluent, zero-dependency assertions over <see cref="Result{T}"/> and <see cref="Result"/>.</summary>
public static class ResultAssertions
{
    /// <summary>Asserts success and returns the value (terminal).</summary>
    public static T ShouldBeSuccess<T>(this Result<T> result)
    {
        if (result.TryGetValue(out var value))
        {
            return value;
        }

        throw Fail("Expected a successful result, but it failed.", result.Errors);
    }

    /// <summary>Asserts success (valueless) and returns the result for chaining.</summary>
    public static Result ShouldBeSuccess(this Result result)
    {
        if (result.IsError)
        {
            throw Fail("Expected a successful result, but it failed.", result.Errors);
        }

        return result;
    }

    /// <summary>Asserts that some error carries <paramref name="code"/>; returns the result for chaining.</summary>
    public static Result<T> ShouldHaveError<T>(this Result<T> result, string code)
    {
        AssertHasError(result.IsError, result.Errors, e => e.Code == code, $"error with code '{code}'");
        return result;
    }

    /// <summary>Asserts that some error carries <paramref name="code"/>; returns the result for chaining.</summary>
    public static Result ShouldHaveError(this Result result, string code)
    {
        AssertHasError(result.IsError, result.Errors, e => e.Code == code, $"error with code '{code}'");
        return result;
    }

    /// <summary>Asserts that some error is of <paramref name="type"/>; returns the result for chaining.</summary>
    public static Result<T> ShouldHaveError<T>(this Result<T> result, ErrorType type)
    {
        AssertHasError(result.IsError, result.Errors, e => e.Type == type, $"error of type '{type}'");
        return result;
    }

    /// <summary>Asserts that some error is of <paramref name="type"/>; returns the result for chaining.</summary>
    public static Result ShouldHaveError(this Result result, ErrorType type)
    {
        AssertHasError(result.IsError, result.Errors, e => e.Type == type, $"error of type '{type}'");
        return result;
    }

    /// <summary>Asserts success then runs <paramref name="assertion"/> on the value; returns the result.</summary>
    public static Result<T> Assert<T>(this Result<T> result, Action<T> assertion)
    {
        var value = result.ShouldBeSuccess();
        assertion(value);
        return result;
    }

    /// <summary>Asserts a NotFound error.</summary>
    public static Result<T> ShouldBeNotFound<T>(this Result<T> result) => result.ShouldHaveError(ErrorType.NotFound);
    /// <summary>Asserts a NotFound error.</summary>
    public static Result ShouldBeNotFound(this Result result) => result.ShouldHaveError(ErrorType.NotFound);
    /// <summary>Asserts a Validation error.</summary>
    public static Result<T> ShouldBeValidation<T>(this Result<T> result) => result.ShouldHaveError(ErrorType.Validation);
    /// <summary>Asserts a Validation error.</summary>
    public static Result ShouldBeValidation(this Result result) => result.ShouldHaveError(ErrorType.Validation);
    /// <summary>Asserts a Conflict error.</summary>
    public static Result<T> ShouldBeConflict<T>(this Result<T> result) => result.ShouldHaveError(ErrorType.Conflict);
    /// <summary>Asserts a Conflict error.</summary>
    public static Result ShouldBeConflict(this Result result) => result.ShouldHaveError(ErrorType.Conflict);
    /// <summary>Asserts an Unauthorized error.</summary>
    public static Result<T> ShouldBeUnauthorized<T>(this Result<T> result) => result.ShouldHaveError(ErrorType.Unauthorized);
    /// <summary>Asserts an Unauthorized error.</summary>
    public static Result ShouldBeUnauthorized(this Result result) => result.ShouldHaveError(ErrorType.Unauthorized);
    /// <summary>Asserts a Forbidden error.</summary>
    public static Result<T> ShouldBeForbidden<T>(this Result<T> result) => result.ShouldHaveError(ErrorType.Forbidden);
    /// <summary>Asserts a Forbidden error.</summary>
    public static Result ShouldBeForbidden(this Result result) => result.ShouldHaveError(ErrorType.Forbidden);
    /// <summary>Asserts an Unexpected error.</summary>
    public static Result<T> ShouldBeUnexpected<T>(this Result<T> result) => result.ShouldHaveError(ErrorType.Unexpected);
    /// <summary>Asserts an Unexpected error.</summary>
    public static Result ShouldBeUnexpected(this Result result) => result.ShouldHaveError(ErrorType.Unexpected);

    private static void AssertHasError(bool isError, IReadOnlyList<ErrorBase> errors, Func<ErrorBase, bool> predicate, string expectation)
    {
        if (!isError)
        {
            throw Fail($"Expected an {expectation}, but the result was successful.", errors);
        }

        if (!errors.Any(predicate))
        {
            throw Fail($"Expected an {expectation}, but none matched.", errors);
        }
    }

    private static VostraAssertionException Fail(string headline, IReadOnlyList<ErrorBase> errors) =>
        new(headline + Environment.NewLine + Describe(errors));

    /// <summary>Composes the rich diagnostic: each error's identity plus any attached request context.</summary>
    internal static string Describe(IReadOnlyList<ErrorBase> errors)
    {
        if (errors.Count == 0)
        {
            return "  (no errors)";
        }

        var builder = new StringBuilder();
        foreach (var error in errors)
        {
            builder.Append("  - ").Append(error.Type).Append(' ').Append(error.Code).Append(": ").Append(error.Message);
            if (error.Metadata is { } metadata && metadata.TryGetValue("request", out var raw) && raw is RequestContext request)
            {
                builder.Append(Environment.NewLine).Append("    request: ")
                    .Append(request.Verb).Append(' ').Append(request.Url);
                if (request.Body is not null)
                {
                    builder.Append(" | body: ").Append(request.Body);
                }
            }

            builder.Append(Environment.NewLine);
        }

        return builder.ToString().TrimEnd();
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run:
```bash
dotnet test tests/Vostra.Results.Testing.Tests --filter ResultAssertionsTests
```
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/Vostra.Results.Testing/VostraAssertionException.cs src/Vostra.Results.Testing/ResultAssertions.cs tests/Vostra.Results.Testing.Tests/ResultAssertionsTests.cs
git commit -m "feat(testing): add VostraAssertionException and fluent sync assertions"
```

---

### Task 6: Asynchronous assertions (`Task<Result<T>>` / `Task<Result>`)

**Files:**
- Create: `src/Vostra.Results.Testing/ResultTaskAssertions.cs`
- Create: `tests/Vostra.Results.Testing.Tests/ResultTaskAssertionsTests.cs`

**Interfaces:**
- Consumes: the synchronous `ResultAssertions` extension methods.
- Produces: `public static class ResultTaskAssertions` — `await`-then-delegate overloads so `await api.X(...).ShouldBeSuccess()` compiles with no stray `await`:
  - `Task<T> ShouldBeSuccess<T>(this Task<Result<T>> task)`
  - `Task<Result> ShouldBeSuccess(this Task<Result> task)`
  - `Task<Result<T>> ShouldHaveError<T>(this Task<Result<T>> task, string code)` and the `ErrorType` overload
  - `Task<Result> ShouldHaveError(this Task<Result> task, string code)` and the `ErrorType` overload
  - `Task<Result<T>> Assert<T>(this Task<Result<T>> task, Action<T> assertion)`
  - async kind sugar for both `Task<Result<T>>` and `Task<Result>`.

- [ ] **Step 1: Write the failing tests**

`tests/Vostra.Results.Testing.Tests/ResultTaskAssertionsTests.cs`:
```csharp
namespace Vostra.Results.Testing.Tests;

public class ResultTaskAssertionsTests
{
    private static Task<Result<int>> OkAsync(int v) => Task.FromResult<Result<int>>(v);
    private static Task<Result<int>> FailAsync() => Task.FromResult<Result<int>>(new NotFoundError("nope", "X.NotFound"));

    [Fact]
    public async Task ShouldBeSuccess_awaits_and_returns_value()
    {
        var value = await OkAsync(7).ShouldBeSuccess();

        value.Should().Be(7);
    }

    [Fact]
    public async Task ShouldHaveError_awaits_and_chains()
    {
        var chained = await FailAsync().ShouldHaveError("X.NotFound");

        chained.IsError.Should().BeTrue();
    }

    [Fact]
    public async Task ShouldBeNotFound_async_sugar_passes()
    {
        await FailAsync().ShouldBeNotFound();
    }

    [Fact]
    public async Task Assert_async_runs_and_chains()
    {
        var seen = 0;

        await OkAsync(9).Assert(v => seen = v);

        seen.Should().Be(9);
    }

    [Fact]
    public async Task ShouldBeSuccess_async_throws_on_failure()
    {
        var act = async () => await FailAsync().ShouldBeSuccess();

        await act.Should().ThrowAsync<VostraAssertionException>();
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run:
```bash
dotnet test tests/Vostra.Results.Testing.Tests --filter ResultTaskAssertionsTests
```
Expected: FAIL — `ResultTaskAssertions` does not exist.

- [ ] **Step 3: Implement `ResultTaskAssertions`**

`src/Vostra.Results.Testing/ResultTaskAssertions.cs`:
```csharp
using Vostra.Results;

namespace Vostra.Results.Testing;

/// <summary>Async overloads of <see cref="ResultAssertions"/> for <see cref="Task{TResult}"/>-wrapped results,
/// so call sites need no stray <c>await</c>.</summary>
public static class ResultTaskAssertions
{
    /// <summary>Awaits then asserts success, returning the value (terminal).</summary>
    public static async Task<T> ShouldBeSuccess<T>(this Task<Result<T>> task) =>
        (await task.ConfigureAwait(false)).ShouldBeSuccess();

    /// <summary>Awaits then asserts success (valueless), returning the result.</summary>
    public static async Task<Result> ShouldBeSuccess(this Task<Result> task) =>
        (await task.ConfigureAwait(false)).ShouldBeSuccess();

    /// <summary>Awaits then asserts an error with <paramref name="code"/>.</summary>
    public static async Task<Result<T>> ShouldHaveError<T>(this Task<Result<T>> task, string code) =>
        (await task.ConfigureAwait(false)).ShouldHaveError(code);

    /// <summary>Awaits then asserts an error of <paramref name="type"/>.</summary>
    public static async Task<Result<T>> ShouldHaveError<T>(this Task<Result<T>> task, ErrorType type) =>
        (await task.ConfigureAwait(false)).ShouldHaveError(type);

    /// <summary>Awaits then asserts an error with <paramref name="code"/>.</summary>
    public static async Task<Result> ShouldHaveError(this Task<Result> task, string code) =>
        (await task.ConfigureAwait(false)).ShouldHaveError(code);

    /// <summary>Awaits then asserts an error of <paramref name="type"/>.</summary>
    public static async Task<Result> ShouldHaveError(this Task<Result> task, ErrorType type) =>
        (await task.ConfigureAwait(false)).ShouldHaveError(type);

    /// <summary>Awaits then asserts success and runs <paramref name="assertion"/> on the value.</summary>
    public static async Task<Result<T>> Assert<T>(this Task<Result<T>> task, Action<T> assertion) =>
        (await task.ConfigureAwait(false)).Assert(assertion);

    /// <summary>Awaits then asserts a NotFound error.</summary>
    public static async Task<Result<T>> ShouldBeNotFound<T>(this Task<Result<T>> task) =>
        (await task.ConfigureAwait(false)).ShouldBeNotFound();
    /// <summary>Awaits then asserts a NotFound error.</summary>
    public static async Task<Result> ShouldBeNotFound(this Task<Result> task) =>
        (await task.ConfigureAwait(false)).ShouldBeNotFound();
    /// <summary>Awaits then asserts a Validation error.</summary>
    public static async Task<Result<T>> ShouldBeValidation<T>(this Task<Result<T>> task) =>
        (await task.ConfigureAwait(false)).ShouldBeValidation();
    /// <summary>Awaits then asserts a Validation error.</summary>
    public static async Task<Result> ShouldBeValidation(this Task<Result> task) =>
        (await task.ConfigureAwait(false)).ShouldBeValidation();
    /// <summary>Awaits then asserts a Conflict error.</summary>
    public static async Task<Result<T>> ShouldBeConflict<T>(this Task<Result<T>> task) =>
        (await task.ConfigureAwait(false)).ShouldBeConflict();
    /// <summary>Awaits then asserts a Conflict error.</summary>
    public static async Task<Result> ShouldBeConflict(this Task<Result> task) =>
        (await task.ConfigureAwait(false)).ShouldBeConflict();
    /// <summary>Awaits then asserts an Unauthorized error.</summary>
    public static async Task<Result<T>> ShouldBeUnauthorized<T>(this Task<Result<T>> task) =>
        (await task.ConfigureAwait(false)).ShouldBeUnauthorized();
    /// <summary>Awaits then asserts an Unauthorized error.</summary>
    public static async Task<Result> ShouldBeUnauthorized(this Task<Result> task) =>
        (await task.ConfigureAwait(false)).ShouldBeUnauthorized();
    /// <summary>Awaits then asserts a Forbidden error.</summary>
    public static async Task<Result<T>> ShouldBeForbidden<T>(this Task<Result<T>> task) =>
        (await task.ConfigureAwait(false)).ShouldBeForbidden();
    /// <summary>Awaits then asserts a Forbidden error.</summary>
    public static async Task<Result> ShouldBeForbidden(this Task<Result> task) =>
        (await task.ConfigureAwait(false)).ShouldBeForbidden();
    /// <summary>Awaits then asserts an Unexpected error.</summary>
    public static async Task<Result<T>> ShouldBeUnexpected<T>(this Task<Result<T>> task) =>
        (await task.ConfigureAwait(false)).ShouldBeUnexpected();
    /// <summary>Awaits then asserts an Unexpected error.</summary>
    public static async Task<Result> ShouldBeUnexpected(this Task<Result> task) =>
        (await task.ConfigureAwait(false)).ShouldBeUnexpected();
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run:
```bash
dotnet test tests/Vostra.Results.Testing.Tests --filter ResultTaskAssertionsTests
```
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Vostra.Results.Testing/ResultTaskAssertions.cs tests/Vostra.Results.Testing.Tests/ResultTaskAssertionsTests.cs
git commit -m "feat(testing): add async assertion overloads for Task<Result<T>>"
```

---

### Task 7: Round-trip integration tests (real in-process server)

**Files:**
- Create: `tests/Vostra.Results.Testing.Tests/RoundTrip/TestApiHost.cs`
- Create: `tests/Vostra.Results.Testing.Tests/RoundTrip/RoundTripTests.cs`

**Interfaces:**
- Consumes: `TestHttpClient`, the assertion extensions, AspNetCore `AddVostraResults()` + `ToHttpResponse(HttpContext)`.
- Produces: `internal sealed class TestApiHost : IAsyncLifetime` exposing `TestHttpClient Api { get; }` — an in-process minimal-API server (via `Microsoft.AspNetCore.TestHost`) whose endpoints route domain `Result`s through the real `ToHttpResponse`.

> This is the end-to-end proof (acceptance criterion #5): a real HTTP request/response through the actual AspNetCore mapping, consumed by `TestHttpClient`, asserted with no substring matching.

- [ ] **Step 1: Write the in-process host fixture**

`tests/Vostra.Results.Testing.Tests/RoundTrip/TestApiHost.cs`:
```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vostra.Results;
using Vostra.Results.AspNetCore;

namespace Vostra.Results.Testing.Tests.RoundTrip;

/// <summary>Domain payload used by the round-trip endpoints.</summary>
public sealed record Product(int Id, string Name);

/// <summary>Spins up a real in-process server whose endpoints route Results through ToHttpResponse.</summary>
internal sealed class TestApiHost : IAsyncLifetime
{
    private WebApplication? _app;

    public TestHttpClient Api { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddVostraResults();
        var app = builder.Build();

        app.MapGet("/products/{id}", (int id, HttpContext ctx) =>
        {
            Result<Product> result = id == 7
                ? new Product(7, "Widget")
                : new NotFoundError($"Product {id} not found", "Product.NotFound");
            return result.ToHttpResponse(ctx);
        });

        app.MapPost("/products", (Product input, HttpContext ctx) =>
        {
            Result<Product> result = string.IsNullOrWhiteSpace(input.Name)
                ? new ValidationError("Name is required.", "Product.Validation",
                    metadata: new Dictionary<string, object?> { ["field"] = "name" })
                : Result.Created(new Product(99, input.Name));
            return result.ToHttpResponse(ctx);
        });

        app.MapGet("/products", (HttpContext ctx) =>
        {
            // The collection overload extends Result<IEnumerable<T>>, so type the result explicitly
            // (extension-method resolution does not apply the implicit T -> Result<T> conversion to the receiver).
            Result<IEnumerable<Product>> result = new[] { new Product(1, "a"), new Product(2, "b") };
            return result.ToHttpResponse(ctx, new Pagination(1, 20, 2));
        });

        app.MapDelete("/products/{id}", (int id, HttpContext ctx) =>
        {
            Result result = id == 7 ? Result.Success : new NotFoundError("gone", "Product.NotFound");
            return result.ToHttpResponse(ctx);
        });

        await app.StartAsync();
        _app = app;
        Api = new TestHttpClient(app.GetTestClient(), "products");
    }

    public async Task DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }
}
```

> **Verified `ToHttpResponse` overloads** (`src/Vostra.Results.AspNetCore/ToHttpResponseExtensions.cs`): scalar `ToHttpResponse<T>(this Result<T>, HttpContext)`; collection `ToHttpResponse<T>(this Result<IEnumerable<T>>, HttpContext, Pagination)`; non-generic `ToHttpResponse(this Result, HttpContext)`. The host code above matches these exactly. Do **not** change the AspNetCore package.

- [ ] **Step 2: Write the round-trip tests**

`tests/Vostra.Results.Testing.Tests/RoundTrip/RoundTripTests.cs`:
```csharp
namespace Vostra.Results.Testing.Tests.RoundTrip;

public class RoundTripTests : IClassFixture<TestApiHost>
{
    private readonly TestApiHost _host;

    public RoundTripTests(TestApiHost host) => _host = host;

    [Fact]
    public async Task Get_existing_returns_value()
    {
        var product = await _host.Api.Get<Product>("/7").ShouldBeSuccess();

        product.Should().Be(new Product(7, "Widget"));
    }

    [Fact]
    public async Task Get_missing_rebuilds_typed_NotFound_with_code()
    {
        await _host.Api.Get<Product>("/9").ShouldHaveError("Product.NotFound");
    }

    [Fact]
    public async Task Get_missing_is_typed_NotFound()
    {
        var result = await _host.Api.Get<Product>("/9");

        result.FirstError.Should().BeOfType<NotFoundError>();
    }

    [Fact]
    public async Task Post_created_returns_value_with_Created_kind()
    {
        var result = await _host.Api.Post<Product>("", new Product(0, "New"));

        result.SuccessKind.Should().Be(SuccessKind.Created);
        result.ShouldBeSuccess().Name.Should().Be("New");
    }

    [Fact]
    public async Task Post_invalid_rebuilds_validation_error()
    {
        await _host.Api.Post<Product>("", new Product(0, "")).ShouldBeValidation();
    }

    [Fact]
    public async Task GetList_returns_items_and_pagination()
    {
        var page = await _host.Api.GetList<Product>("").ShouldBeSuccess();

        page.Items.Should().HaveCount(2);
        page.Pagination.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task Delete_existing_is_success()
    {
        await _host.Api.Delete("/7").ShouldBeSuccess();
    }

    [Fact]
    public async Task Delete_missing_rebuilds_typed_error()
    {
        await _host.Api.Delete("/9").ShouldHaveError("Product.NotFound");
    }
}
```

- [ ] **Step 3: Run the round-trip tests**

Run:
```bash
dotnet test tests/Vostra.Results.Testing.Tests --filter RoundTripTests
```
Expected: PASS. If a compile error mentions `ToHttpResponse` overload mismatch, fix the call in `TestApiHost.cs` to match the real signature (see implementer note) and re-run.

- [ ] **Step 4: Run the FULL suite**

Run:
```bash
dotnet test tests/Vostra.Results.Testing.Tests
```
Expected: all tests pass on net8.0 and net9.0.

- [ ] **Step 5: Commit**

```bash
git add tests/Vostra.Results.Testing.Tests/RoundTrip
git commit -m "test(testing): prove Core->AspNetCore->Testing round-trip end to end"
```

---

### Task 8: README + package polish

**Files:**
- Create: `src/Vostra.Results.Testing/README.md`
- Modify: `README.md` (repo root — add a short Testing section, matching how Core/AspNetCore are documented)
- Modify: `THIRD-PARTY-NOTICES.md` (only if reference-derived attribution is warranted; the helper was a port of the repo's own `IntegrationTestHelper`, so likely a one-line note)

**Interfaces:** none (docs only).

- [ ] **Step 1: Write the package README**

`src/Vostra.Results.Testing/README.md`:
````markdown
# Vostra.Results.Testing

Integration-testing toolkit for [Vostra.Results](https://www.nuget.org/packages/Vostra.Results). A
`TestHttpClient` collapses an HTTP round-trip into a `Result<T>`, rebuilding the **typed error** from the
`Vostra.Results.AspNetCore` response so tests assert error *identity*, not substrings.

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
````

- [ ] **Step 2: Add a Testing section to the repo README**

Open `README.md` and add (place it after the AspNetCore section, mirroring its formatting):
```markdown
## Vostra.Results.Testing

Integration-testing toolkit: a `TestHttpClient` returning `Result<T>` with typed-error reconstruction, plus
zero-dependency fluent assertions (`ShouldBeSuccess`, `ShouldHaveError`, kind sugar). See
[the package README](src/Vostra.Results.Testing/README.md).
```

- [ ] **Step 3: Build the package to verify README packs**

Run:
```bash
dotnet pack src/Vostra.Results.Testing/Vostra.Results.Testing.csproj -c Release -o ./artifacts
```
Expected: succeeds; produces `artifacts/Vostra.Results.Testing.<version>.nupkg` with no NU5039 (missing README) warning.

- [ ] **Step 4: Run the full solution test suite**

Run:
```bash
git restore Vostra.Results.sln 2>/dev/null; dotnet test Vostra.Results.sln
```
Expected: all projects build and all tests pass (Core, AspNetCore, Testing) on net8.0 + net9.0.

- [ ] **Step 5: Commit**

```bash
git add src/Vostra.Results.Testing/README.md README.md THIRD-PARTY-NOTICES.md
git commit -m "docs(testing): add package README and repo usage docs"
```

---

## Self-Review

**1. Spec coverage**

| Spec item | Task |
|-----------|------|
| FR-11.1 verbs, async, baseUrl, module-agnostic | Task 4 (+ GetList) |
| FR-11.2 rich message (S6) + strict-deser hint (S7) | Task 5 (`Describe`), Task 3 (`RawJsonFormat` hint) |
| FR-11.3 typed-error reconstruction | Task 2 + Task 3 (`ReadErrors`) + Task 7 (proof) |
| FR-11.4 chainable `Assert`/`ShouldBeSuccess`/`ShouldHaveError` | Task 5 + Task 6 |
| FR-11.5 injectable serializer/envelope (`IResultRawFormat`) | Task 3 |
| FR-12 STJ, enum-as-string, case-tolerant | Task 2 (`ParseType` ignoreCase) + Task 3 |
| Created/201 recovery | Task 4 (`Result.Created`) + Task 7 |
| List + pagination | Task 1 (`PagedList`), Task 3 (`ReadList`), Task 4 (`GetList`), Task 7 |
| Dual-shaped `errors` parsing | Task 2 |
| Depend on AspNetCore, reuse envelopes/Pagination | Task 1 (csproj) |
| Round-trip end-to-end (acceptance #5) | Task 7 |
| Docs/attribution | Task 8 |

No gaps found.

**2. Placeholder scan:** No TBD/TODO/"add error handling"/"similar to" placeholders; every code step shows complete code. The one judgement call — matching the real `ToHttpResponse` collection overload signature — is called out explicitly in Task 7 with the file to check, not left vague.

**3. Type consistency:** Verified method/type names are identical across tasks: `IResultRawFormat` (`SerializeRequest`/`ReadData`/`ReadList`/`ReadErrors`), `RawJsonFormat.Instance`, `ProblemDetailsErrorReader.Read`, `TestHttpClient` verb names, `RequestContext(Verb,Url,Body)`, `VostraAssertionException`, `ShouldBeSuccess`/`ShouldHaveError`/`Assert`/kind-sugar names match between sync (Task 5) and async (Task 6). `Result.Created<T>` / `Result.Created()` / `Result.Success` used per the verified Core API.
