# Vostra.Results.AspNetCore Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a `Vostra.Results.AspNetCore` package that maps `Result`/`Result<T>` onto HTTP via `IResult`-returning extension methods, with DI-configured status mapping and an RFC 7807 error wire contract that carries typed-error identity.

**Architecture:** A new class library `src/Vostra.Results.AspNetCore` referencing only the ASP.NET Core shared framework + Core. Service methods return `Result<T>`; controllers/minimal-API handlers call `result.ToHttpResponse(HttpContext)`. Success → a thin `{ operationId, data }` envelope (200/201 from `SuccessKind`); failure → a hand-built `ProblemDetails`/`HttpValidationProblemDetails` carrying `code` + `errorType` + `operationId`. Status comes from the error via a DI-configured, open/closed map (no central switch).

**Tech Stack:** C# (LangVersion latest), net8.0;net9.0, ASP.NET Core (`FrameworkReference Microsoft.AspNetCore.App`), System.Text.Json, xUnit 2.5.3 + FluentAssertions 8.10.0.

**Spec:** [docs/superpowers/specs/2026-06-20-aspnetcore-design.md](../specs/2026-06-20-aspnetcore-design.md)

## Global Constraints

- Target frameworks: `net8.0;net9.0` (every project).
- Dependencies: AspNetCore project references **only** `FrameworkReference Microsoft.AspNetCore.App` + `ProjectReference` to `Vostra.Results`. No other runtime NuGet packages. Core stays HTTP-free (do not touch `src/Vostra.Results`).
- `Directory.Build.props` is inherited: `Nullable=enable`, `ImplicitUsings=enable`, `TreatWarningsAsErrors=true`, `EnforceCodeStyleInBuild=true`. **Every public member needs an XML `///` doc comment** (the library project sets `GenerateDocumentationFile=true`; a missing doc is CS1591 = build error).
- File-scoped namespaces; namespace `Vostra.Results.AspNetCore`.
- Serializer: System.Text.Json. Wire property names are camelCase — set them explicitly with `[JsonPropertyName]` (do not rely on global naming policy).
- Error bodies use content type `application/problem+json`; success bodies use `application/json`.
- **Commit message rule (from CLAUDE.md): never include any `Co-Authored-By` / Claude / AI attribution line in commits.** Plain conventional-commit messages only.
- Status resolution precedence: per-`Code` override → per-`ErrorType` map → built-in default.
- Built-in default status map: Validation→400, Unauthorized→401, Forbidden→403, NotFound→404, Conflict→409, Unexpected→500.

---

## Plan revision (2026-06-20, after Task 4)

The original **Task 5 (standalone `OperationId` helper class + `InternalsVisibleTo`)** was dropped as
over-engineering for a generic library. The operation-id *value* is still stamped on every response
(FR-10.3) — the one line `Activity.Current?.Id ?? http.TraceIdentifier` is now a **private helper inside
`ToHttpResponseExtensions`**, not its own public/internal class, and there is **no `InternalsVisibleTo`**.

Because the only internal helpers (`ProblemResultBuilder`, the inlined operation id) are reachable through
the public `ToHttpResponse` surface, the original **Task 6 (ProblemResultBuilder) and Task 7
(ToHttpResponse extensions) are merged into a single revised Task 5**, tested entirely through the public
`ToHttpResponse` API (no direct calls to internals). Tasks **8 → wire/usage**, **9 → docs**,
**10 → final verification** are unchanged in content (their numbering is retained). The revised Task 5's
authoritative brief is hand-authored at `.superpowers/sdd/task-5-brief.md`; the original Task 5/6/7
sections below are superseded by it.

---

### Task 1: Scaffold project, test project, solution wiring

**Files:**
- Create: `src/Vostra.Results.AspNetCore/Vostra.Results.AspNetCore.csproj`
- Create: `tests/Vostra.Results.AspNetCore.Tests/Vostra.Results.AspNetCore.Tests.csproj`
- Create: `tests/Vostra.Results.AspNetCore.Tests/GlobalUsings.cs`
- Create: `tests/Vostra.Results.AspNetCore.Tests/ScaffoldSmokeTests.cs`
- Modify: `Vostra.Results.sln` (add both projects)

**Interfaces:**
- Consumes: the existing `Vostra.Results` project.
- Produces: a buildable `Vostra.Results.AspNetCore` assembly + a test project that references it.

- [ ] **Step 1: Create the library csproj**

`src/Vostra.Results.AspNetCore/Vostra.Results.AspNetCore.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>net8.0;net9.0</TargetFrameworks>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <PackageId>Vostra.Results.AspNetCore</PackageId>
    <Description>ASP.NET Core HTTP mapping for Vostra.Results — Result&lt;T&gt; to IResult with RFC 7807 ProblemDetails.</Description>
    <PackageReadmeFile>README.md</PackageReadmeFile>
  </PropertyGroup>

  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
    <ProjectReference Include="..\Vostra.Results\Vostra.Results.csproj" />
  </ItemGroup>

  <ItemGroup>
    <None Include="README.md" Pack="true" PackagePath="\" Condition="Exists('README.md')" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Create the test csproj**

`tests/Vostra.Results.AspNetCore.Tests/Vostra.Results.AspNetCore.Tests.csproj`:

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
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="xunit" Version="2.5.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.3" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\Vostra.Results.AspNetCore\Vostra.Results.AspNetCore.csproj" />
  </ItemGroup>
</Project>
```

Note: the test project needs `FrameworkReference Microsoft.AspNetCore.App` so tests can use `DefaultHttpContext`, `Results`, and `IServiceCollection`.

- [ ] **Step 3: Create test GlobalUsings**

`tests/Vostra.Results.AspNetCore.Tests/GlobalUsings.cs`:

```csharp
global using FluentAssertions;
global using Vostra.Results;
global using Vostra.Results.AspNetCore;
global using Xunit;
```

- [ ] **Step 4: Write the smoke test**

`tests/Vostra.Results.AspNetCore.Tests/ScaffoldSmokeTests.cs`:

```csharp
namespace Vostra.Results.AspNetCore.Tests;

public class ScaffoldSmokeTests
{
    [Fact]
    public void Core_types_are_referenced()
    {
        Result<int> ok = 7;
        ok.IsSuccess.Should().BeTrue();
    }
}
```

- [ ] **Step 5: Add both projects to the solution**

Run:
```bash
dotnet sln Vostra.Results.sln add src/Vostra.Results.AspNetCore/Vostra.Results.AspNetCore.csproj
dotnet sln Vostra.Results.sln add tests/Vostra.Results.AspNetCore.Tests/Vostra.Results.AspNetCore.Tests.csproj
```

- [ ] **Step 6: Build and run the smoke test**

Run: `dotnet test tests/Vostra.Results.AspNetCore.Tests/Vostra.Results.AspNetCore.Tests.csproj`
Expected: build succeeds, 1 test passes (×2 frameworks).

- [ ] **Step 7: Commit**

```bash
git add src/Vostra.Results.AspNetCore tests/Vostra.Results.AspNetCore.Tests Vostra.Results.sln
git commit -m "feat(aspnetcore): scaffold Vostra.Results.AspNetCore project and tests"
```

---

### Task 2: Envelopes and Pagination

**Files:**
- Create: `src/Vostra.Results.AspNetCore/Envelopes.cs`
- Test: `tests/Vostra.Results.AspNetCore.Tests/EnvelopeTests.cs`

**Interfaces:**
- Produces:
  - `public sealed record Pagination(int Page, int PageSize, long TotalCount) { public int TotalPages { get; } }`
  - `public sealed class SuccessEnvelope<T> { public string? OperationId { get; init; } public T? Data { get; init; } }`
  - `public sealed class ListEnvelope<T> { public string? OperationId { get; init; } public IReadOnlyList<T> Data { get; init; } public Pagination Pagination { get; init; } }`

- [ ] **Step 1: Write the failing test**

`tests/Vostra.Results.AspNetCore.Tests/EnvelopeTests.cs`:

```csharp
using System.Text.Json;

namespace Vostra.Results.AspNetCore.Tests;

public class EnvelopeTests
{
    [Theory]
    [InlineData(20, 137, 7)]   // 137/20 -> 7 pages
    [InlineData(20, 140, 7)]   // exact multiple
    [InlineData(20, 0, 0)]     // empty
    [InlineData(0, 137, 0)]    // pageSize 0 -> no div-by-zero
    public void Pagination_TotalPages_is_computed(int pageSize, long total, int expected)
    {
        new Pagination(1, pageSize, total).TotalPages.Should().Be(expected);
    }

    [Fact]
    public void SuccessEnvelope_serializes_camelCase_and_omits_null_data()
    {
        var json = JsonSerializer.Serialize(new SuccessEnvelope<object?> { OperationId = "op1", Data = null });
        json.Should().Contain("\"operationId\":\"op1\"");
        json.Should().NotContain("data");
    }

    [Fact]
    public void ListEnvelope_serializes_data_and_pagination()
    {
        var env = new ListEnvelope<int>
        {
            OperationId = "op2",
            Data = new[] { 1, 2 },
            Pagination = new Pagination(1, 20, 2),
        };
        var json = JsonSerializer.Serialize(env);
        json.Should().Contain("\"operationId\":\"op2\"");
        json.Should().Contain("\"data\":[1,2]");
        json.Should().Contain("\"pagination\":");
        json.Should().Contain("\"totalCount\":2");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Vostra.Results.AspNetCore.Tests --filter EnvelopeTests`
Expected: FAIL (types `Pagination`/`SuccessEnvelope`/`ListEnvelope` not found).

- [ ] **Step 3: Implement the envelopes**

`src/Vostra.Results.AspNetCore/Envelopes.cs`:

```csharp
using System.Text.Json.Serialization;

namespace Vostra.Results.AspNetCore;

/// <summary>Pagination metadata for a list response.</summary>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Items per page.</param>
/// <param name="TotalCount">Total items across all pages.</param>
public sealed record Pagination(
    [property: JsonPropertyName("page")] int Page,
    [property: JsonPropertyName("pageSize")] int PageSize,
    [property: JsonPropertyName("totalCount")] long TotalCount)
{
    /// <summary>Total number of pages; 0 when <see cref="PageSize"/> is not positive.</summary>
    [JsonPropertyName("totalPages")]
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

/// <summary>Thin success envelope carrying the operation id and the payload.</summary>
/// <typeparam name="T">The payload type.</typeparam>
public sealed class SuccessEnvelope<T>
{
    /// <summary>Correlation/operation id for the request.</summary>
    [JsonPropertyName("operationId")]
    public string? OperationId { get; init; }

    /// <summary>The response payload; omitted from JSON when null.</summary>
    [JsonPropertyName("data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public T? Data { get; init; }
}

/// <summary>Success envelope for a paginated list.</summary>
/// <typeparam name="T">The element type.</typeparam>
public sealed class ListEnvelope<T>
{
    /// <summary>Correlation/operation id for the request.</summary>
    [JsonPropertyName("operationId")]
    public string? OperationId { get; init; }

    /// <summary>The page of items.</summary>
    [JsonPropertyName("data")]
    public IReadOnlyList<T> Data { get; init; } = Array.Empty<T>();

    /// <summary>Pagination metadata.</summary>
    [JsonPropertyName("pagination")]
    public Pagination Pagination { get; init; } = new(1, 0, 0);
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/Vostra.Results.AspNetCore.Tests --filter EnvelopeTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Vostra.Results.AspNetCore/Envelopes.cs tests/Vostra.Results.AspNetCore.Tests/EnvelopeTests.cs
git commit -m "feat(aspnetcore): add SuccessEnvelope, ListEnvelope, Pagination"
```

---

### Task 3: Default status map, options, and status resolver

**Files:**
- Create: `src/Vostra.Results.AspNetCore/DefaultStatusMap.cs`
- Create: `src/Vostra.Results.AspNetCore/VostraResultsOptions.cs`
- Create: `src/Vostra.Results.AspNetCore/ErrorStatusResolver.cs`
- Test: `tests/Vostra.Results.AspNetCore.Tests/StatusResolutionTests.cs`

**Interfaces:**
- Consumes: `Vostra.Results.ErrorType`, `Vostra.Results.ErrorBase`.
- Produces:
  - `public static class DefaultStatusMap { public static int ForType(ErrorType type); public static IReadOnlyDictionary<ErrorType,int> Defaults { get; } }`
  - `public sealed class VostraResultsOptions { public static VostraResultsOptions Default { get; } public VostraResultsOptions MapStatus(ErrorType type, int status); public VostraResultsOptions MapStatusForCode(string code, int status); internal int ResolveStatus(ErrorBase error); }`
  - `public static class ErrorStatusResolver { public static int Resolve(ErrorBase error, VostraResultsOptions options); }`

- [ ] **Step 1: Write the failing test**

`tests/Vostra.Results.AspNetCore.Tests/StatusResolutionTests.cs`:

```csharp
namespace Vostra.Results.AspNetCore.Tests;

public class StatusResolutionTests
{
    [Theory]
    [InlineData(ErrorType.Validation, 400)]
    [InlineData(ErrorType.Unauthorized, 401)]
    [InlineData(ErrorType.Forbidden, 403)]
    [InlineData(ErrorType.NotFound, 404)]
    [InlineData(ErrorType.Conflict, 409)]
    [InlineData(ErrorType.Unexpected, 500)]
    public void Default_map_covers_every_error_type(ErrorType type, int expected)
    {
        DefaultStatusMap.ForType(type).Should().Be(expected);
    }

    [Fact]
    public void Unconfigured_options_use_defaults()
    {
        ErrorStatusResolver.Resolve(new NotFoundError("x"), VostraResultsOptions.Default).Should().Be(404);
    }

    [Fact]
    public void MapStatus_overrides_a_whole_type()
    {
        var opts = new VostraResultsOptions().MapStatus(ErrorType.Conflict, 422);
        ErrorStatusResolver.Resolve(new ConflictError("x"), opts).Should().Be(422);
    }

    [Fact]
    public void MapStatusForCode_beats_the_type_map()
    {
        var opts = new VostraResultsOptions()
            .MapStatus(ErrorType.Conflict, 422)
            .MapStatusForCode("Order.Locked", 423);
        var error = new ConflictError("locked", code: "Order.Locked");
        ErrorStatusResolver.Resolve(error, opts).Should().Be(423);
    }

    [Fact]
    public void New_subclass_maps_through_its_error_type_with_no_config()
    {
        ErrorStatusResolver.Resolve(new CustomTeapotError(), VostraResultsOptions.Default).Should().Be(404);
    }

    private sealed class CustomTeapotError : ErrorBase
    {
        public CustomTeapotError() : base("Custom.Teapot", "I'm a teapot", ErrorType.NotFound) { }
        protected override ErrorBase CloneWith(string code, string message, Exception? causedBy, IReadOnlyDictionary<string, object?>? metadata)
            => new CustomTeapotError();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Vostra.Results.AspNetCore.Tests --filter StatusResolutionTests`
Expected: FAIL (types not found).

- [ ] **Step 3: Implement DefaultStatusMap**

`src/Vostra.Results.AspNetCore/DefaultStatusMap.cs`:

```csharp
namespace Vostra.Results.AspNetCore;

/// <summary>
/// The built-in <see cref="ErrorType"/> → HTTP status map. This is the shared contract the
/// Testing package consumes to rebuild typed errors from the wire (do not let it drift).
/// </summary>
public static class DefaultStatusMap
{
    private static readonly IReadOnlyDictionary<ErrorType, int> _defaults = new Dictionary<ErrorType, int>
    {
        [ErrorType.Validation] = 400,
        [ErrorType.Unauthorized] = 401,
        [ErrorType.Forbidden] = 403,
        [ErrorType.NotFound] = 404,
        [ErrorType.Conflict] = 409,
        [ErrorType.Unexpected] = 500,
    };

    /// <summary>The default status for each <see cref="ErrorType"/>.</summary>
    public static IReadOnlyDictionary<ErrorType, int> Defaults => _defaults;

    /// <summary>The default HTTP status for an <see cref="ErrorType"/>; 500 for anything unmapped.</summary>
    public static int ForType(ErrorType type) => _defaults.TryGetValue(type, out var status) ? status : 500;
}
```

- [ ] **Step 4: Implement VostraResultsOptions**

`src/Vostra.Results.AspNetCore/VostraResultsOptions.cs`:

```csharp
namespace Vostra.Results.AspNetCore;

/// <summary>
/// Configuration for HTTP mapping. Override the status for a whole <see cref="ErrorType"/>
/// or for an individual error <c>Code</c>. Resolution precedence: code override → type map → default.
/// </summary>
public sealed class VostraResultsOptions
{
    private readonly Dictionary<ErrorType, int> _byType = new();
    private readonly Dictionary<string, int> _byCode = new(StringComparer.Ordinal);

    /// <summary>A shared, unconfigured instance using only the built-in defaults.</summary>
    public static VostraResultsOptions Default { get; } = new();

    /// <summary>Overrides the HTTP status for a whole <see cref="ErrorType"/>. Returns this for chaining.</summary>
    public VostraResultsOptions MapStatus(ErrorType type, int status)
    {
        _byType[type] = status;
        return this;
    }

    /// <summary>Overrides the HTTP status for a single error <c>Code</c>. Returns this for chaining.</summary>
    public VostraResultsOptions MapStatusForCode(string code, int status)
    {
        _byCode[code] = status;
        return this;
    }

    /// <summary>Resolves the status for an error applying the documented precedence.</summary>
    internal int ResolveStatus(ErrorBase error)
    {
        if (_byCode.TryGetValue(error.Code, out var byCode))
        {
            return byCode;
        }

        return _byType.TryGetValue(error.Type, out var byType) ? byType : DefaultStatusMap.ForType(error.Type);
    }
}
```

- [ ] **Step 5: Implement ErrorStatusResolver**

`src/Vostra.Results.AspNetCore/ErrorStatusResolver.cs`:

```csharp
namespace Vostra.Results.AspNetCore;

/// <summary>Resolves an <see cref="ErrorBase"/> to an HTTP status using the supplied options.</summary>
public static class ErrorStatusResolver
{
    /// <summary>Resolves the HTTP status for <paramref name="error"/> (code override → type map → default).</summary>
    public static int Resolve(ErrorBase error, VostraResultsOptions options) => options.ResolveStatus(error);
}
```

- [ ] **Step 6: Run test to verify it passes**

Run: `dotnet test tests/Vostra.Results.AspNetCore.Tests --filter StatusResolutionTests`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add src/Vostra.Results.AspNetCore/DefaultStatusMap.cs src/Vostra.Results.AspNetCore/VostraResultsOptions.cs src/Vostra.Results.AspNetCore/ErrorStatusResolver.cs tests/Vostra.Results.AspNetCore.Tests/StatusResolutionTests.cs
git commit -m "feat(aspnetcore): add status map, options, and resolver with precedence"
```

---

### Task 4: AddVostraResults DI registration

**Files:**
- Create: `src/Vostra.Results.AspNetCore/ServiceCollectionExtensions.cs`
- Test: `tests/Vostra.Results.AspNetCore.Tests/ServiceRegistrationTests.cs`

**Interfaces:**
- Consumes: `VostraResultsOptions` (Task 3), `Microsoft.Extensions.DependencyInjection.IServiceCollection`.
- Produces: `public static IServiceCollection AddVostraResults(this IServiceCollection services, Action<VostraResultsOptions>? configure = null)` — registers a configured `VostraResultsOptions` singleton.

- [ ] **Step 1: Write the failing test**

`tests/Vostra.Results.AspNetCore.Tests/ServiceRegistrationTests.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;

namespace Vostra.Results.AspNetCore.Tests;

public class ServiceRegistrationTests
{
    [Fact]
    public void AddVostraResults_registers_configured_options()
    {
        var provider = new ServiceCollection()
            .AddVostraResults(o => o.MapStatus(ErrorType.Conflict, 422))
            .BuildServiceProvider();

        var options = provider.GetRequiredService<VostraResultsOptions>();
        ErrorStatusResolver.Resolve(new ConflictError("x"), options).Should().Be(422);
    }

    [Fact]
    public void AddVostraResults_without_configure_registers_default_behavior()
    {
        var provider = new ServiceCollection().AddVostraResults().BuildServiceProvider();

        var options = provider.GetRequiredService<VostraResultsOptions>();
        ErrorStatusResolver.Resolve(new NotFoundError("x"), options).Should().Be(404);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Vostra.Results.AspNetCore.Tests --filter ServiceRegistrationTests`
Expected: FAIL (`AddVostraResults` not found).

- [ ] **Step 3: Implement the extension**

`src/Vostra.Results.AspNetCore/ServiceCollectionExtensions.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;

namespace Vostra.Results.AspNetCore;

/// <summary>DI registration for Vostra.Results HTTP mapping.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers a <see cref="VostraResultsOptions"/> singleton, optionally configured. Calling this is
    /// optional — <c>ToHttpResponse</c> falls back to <see cref="VostraResultsOptions.Default"/> when absent.
    /// </summary>
    public static IServiceCollection AddVostraResults(
        this IServiceCollection services,
        Action<VostraResultsOptions>? configure = null)
    {
        var options = new VostraResultsOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);
        return services;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/Vostra.Results.AspNetCore.Tests --filter ServiceRegistrationTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Vostra.Results.AspNetCore/ServiceCollectionExtensions.cs tests/Vostra.Results.AspNetCore.Tests/ServiceRegistrationTests.cs
git commit -m "feat(aspnetcore): add AddVostraResults DI registration"
```

---

### Task 5: OperationId helper

**Files:**
- Create: `src/Vostra.Results.AspNetCore/OperationId.cs`
- Test: `tests/Vostra.Results.AspNetCore.Tests/OperationIdTests.cs`

**Interfaces:**
- Consumes: `Microsoft.AspNetCore.Http.HttpContext`.
- Produces: `internal static class OperationId { public static string For(HttpContext http); }` — returns `Activity.Current?.Id ?? http.TraceIdentifier`.

Note: `OperationId` is `internal`. Add `InternalsVisibleTo` so the test project can call it.

- [ ] **Step 1: Add InternalsVisibleTo to the library**

Append to `src/Vostra.Results.AspNetCore/Vostra.Results.AspNetCore.csproj` inside a new `<ItemGroup>`:

```xml
  <ItemGroup>
    <InternalsVisibleTo Include="Vostra.Results.AspNetCore.Tests" />
  </ItemGroup>
```

- [ ] **Step 2: Write the failing test**

`tests/Vostra.Results.AspNetCore.Tests/OperationIdTests.cs`:

```csharp
using System.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace Vostra.Results.AspNetCore.Tests;

public class OperationIdTests
{
    [Fact]
    public void Falls_back_to_TraceIdentifier_when_no_activity()
    {
        // Ensure no ambient activity for this assertion.
        var previous = Activity.Current;
        Activity.Current = null;
        try
        {
            var ctx = new DefaultHttpContext { TraceIdentifier = "trace-123" };
            OperationId.For(ctx).Should().Be("trace-123");
        }
        finally
        {
            Activity.Current = previous;
        }
    }

    [Fact]
    public void Prefers_the_current_activity_id()
    {
        using var activity = new Activity("test").Start();
        var ctx = new DefaultHttpContext { TraceIdentifier = "trace-123" };
        OperationId.For(ctx).Should().Be(activity.Id);
    }
}
```

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test tests/Vostra.Results.AspNetCore.Tests --filter OperationIdTests`
Expected: FAIL (`OperationId` not found).

- [ ] **Step 4: Implement the helper**

`src/Vostra.Results.AspNetCore/OperationId.cs`:

```csharp
using System.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace Vostra.Results.AspNetCore;

/// <summary>Computes the operation/correlation id stamped onto every response body.</summary>
internal static class OperationId
{
    /// <summary>The current activity id, or the request's trace identifier as a fallback.</summary>
    public static string For(HttpContext http) => Activity.Current?.Id ?? http.TraceIdentifier;
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test tests/Vostra.Results.AspNetCore.Tests --filter OperationIdTests`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/Vostra.Results.AspNetCore/Vostra.Results.AspNetCore.csproj src/Vostra.Results.AspNetCore/OperationId.cs tests/Vostra.Results.AspNetCore.Tests/OperationIdTests.cs
git commit -m "feat(aspnetcore): add operation id helper"
```

---

### Task 6: ProblemResultBuilder (error IResult)

**Files:**
- Create: `src/Vostra.Results.AspNetCore/ProblemResultBuilder.cs`
- Create: `tests/Vostra.Results.AspNetCore.Tests/HttpResultHarness.cs` (shared test helper)
- Test: `tests/Vostra.Results.AspNetCore.Tests/ProblemResultBuilderTests.cs`

**Interfaces:**
- Consumes: `ErrorBase`, `ErrorType`, `VostraResultsOptions`, `ErrorStatusResolver`.
- Produces: `internal static class ProblemResultBuilder { public static IResult Build(IReadOnlyList<ErrorBase> errors, VostraResultsOptions options, string operationId); }`
  - All errors `ErrorType.Validation` → `HttpValidationProblemDetails` (field map). Otherwise → `ProblemDetails`; multi-error adds an `errors` array extension.
  - Always sets `type="about:blank"`, `title`=reason phrase, status, `code`, `errorType`, `operationId`; content type `application/problem+json`.

- [ ] **Step 1: Write the shared HTTP result harness**

`tests/Vostra.Results.AspNetCore.Tests/HttpResultHarness.cs`:

```csharp
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Vostra.Results.AspNetCore.Tests;

/// <summary>Executes an <see cref="IResult"/> against a real <see cref="DefaultHttpContext"/> and captures it.</summary>
internal static class HttpResultHarness
{
    public static async Task<ExecutedResponse> Execute(IResult result, IServiceProvider? services = null)
    {
        var ctx = new DefaultHttpContext();
        ctx.RequestServices = services ?? new ServiceCollection().BuildServiceProvider();
        ctx.TraceIdentifier = "trace-abc";
        var body = new MemoryStream();
        ctx.Response.Body = body;

        await result.ExecuteAsync(ctx);

        body.Position = 0;
        var text = await new StreamReader(body).ReadToEndAsync();
        return new ExecutedResponse(ctx.Response.StatusCode, ctx.Response.ContentType, text);
    }
}

internal sealed record ExecutedResponse(int Status, string? ContentType, string Body)
{
    public JsonElement Json => JsonDocument.Parse(Body).RootElement;
}
```

- [ ] **Step 2: Write the failing test**

`tests/Vostra.Results.AspNetCore.Tests/ProblemResultBuilderTests.cs`:

```csharp
using System.Text.Json;

namespace Vostra.Results.AspNetCore.Tests;

public class ProblemResultBuilderTests
{
    [Fact]
    public async Task Single_error_renders_problem_details_with_code_and_errorType()
    {
        var result = ProblemResultBuilder.Build(
            new ErrorBase[] { new NotFoundError("Order 42 not found", code: "Order.NotFound") },
            VostraResultsOptions.Default,
            "op-1");

        var res = await HttpResultHarness.Execute(result);

        res.Status.Should().Be(404);
        res.ContentType.Should().StartWith("application/problem+json");
        res.Json.GetProperty("status").GetInt32().Should().Be(404);
        res.Json.GetProperty("type").GetString().Should().Be("about:blank");
        res.Json.GetProperty("title").GetString().Should().Be("Not Found");
        res.Json.GetProperty("detail").GetString().Should().Be("Order 42 not found");
        res.Json.GetProperty("code").GetString().Should().Be("Order.NotFound");
        res.Json.GetProperty("errorType").GetString().Should().Be("NotFound");
        res.Json.GetProperty("operationId").GetString().Should().Be("op-1");
    }

    [Fact]
    public async Task Multiple_non_validation_errors_use_first_for_status_and_list_all()
    {
        var result = ProblemResultBuilder.Build(
            new ErrorBase[]
            {
                new ConflictError("Order is locked", code: "Order.Locked"),
                new Error("Version mismatch", code: "Order.Stale"),
            },
            VostraResultsOptions.Default,
            "op-2");

        var res = await HttpResultHarness.Execute(result);

        res.Status.Should().Be(409);
        res.Json.GetProperty("code").GetString().Should().Be("Order.Locked");
        var errors = res.Json.GetProperty("errors");
        errors.ValueKind.Should().Be(JsonValueKind.Array);
        errors.GetArrayLength().Should().Be(2);
        errors[0].GetProperty("code").GetString().Should().Be("Order.Locked");
        errors[1].GetProperty("code").GetString().Should().Be("Order.Stale");
    }

    [Fact]
    public async Task All_validation_errors_render_field_map()
    {
        var skuField = new Dictionary<string, object?> { ["field"] = "Sku" };
        var priceField = new Dictionary<string, object?> { ["field"] = "Price" };
        var result = ProblemResultBuilder.Build(
            new ErrorBase[]
            {
                new ValidationError("required", metadata: skuField),
                new ValidationError("must be > 0", metadata: priceField),
            },
            VostraResultsOptions.Default,
            "op-3");

        var res = await HttpResultHarness.Execute(result);

        res.Status.Should().Be(400);
        res.ContentType.Should().StartWith("application/problem+json");
        var errors = res.Json.GetProperty("errors");
        errors.GetProperty("Sku")[0].GetString().Should().Be("required");
        errors.GetProperty("Price")[0].GetString().Should().Be("must be > 0");
        res.Json.GetProperty("errorType").GetString().Should().Be("Validation");
    }

    [Fact]
    public async Task Validation_without_field_metadata_keys_by_code()
    {
        var result = ProblemResultBuilder.Build(
            new ErrorBase[] { new ValidationError("required", code: "Sku.Required") },
            VostraResultsOptions.Default,
            "op-4");

        var res = await HttpResultHarness.Execute(result);

        res.Json.GetProperty("errors").GetProperty("Sku.Required")[0].GetString().Should().Be("required");
    }
}
```

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test tests/Vostra.Results.AspNetCore.Tests --filter ProblemResultBuilderTests`
Expected: FAIL (`ProblemResultBuilder` not found).

- [ ] **Step 4: Implement ProblemResultBuilder**

`src/Vostra.Results.AspNetCore/ProblemResultBuilder.cs`:

```csharp
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Vostra.Results.AspNetCore;

/// <summary>Builds the RFC 7807 error <see cref="IResult"/> from one or more errors.</summary>
internal static class ProblemResultBuilder
{
    private const string ProblemJson = "application/problem+json";

    /// <summary>Builds an error response. All-validation errors render a field map; otherwise ProblemDetails.</summary>
    public static IResult Build(IReadOnlyList<ErrorBase> errors, VostraResultsOptions options, string operationId)
    {
        var first = errors[0];
        var status = ErrorStatusResolver.Resolve(first, options);

        if (errors.Count > 0 && AllValidation(errors))
        {
            return BuildValidation(errors, status, operationId);
        }

        var problem = new ProblemDetails
        {
            Type = "about:blank",
            Title = ReasonPhrase(status),
            Status = status,
            Detail = first.Message,
        };
        problem.Extensions["code"] = first.Code;
        problem.Extensions["errorType"] = first.Type.ToString();
        problem.Extensions["operationId"] = operationId;

        if (errors.Count > 1)
        {
            problem.Extensions["errors"] = errors
                .Select(e => new ErrorEntry(e.Code, e.Message))
                .ToArray();
        }

        return Results.Json<ProblemDetails>(problem, statusCode: status, contentType: ProblemJson);
    }

    private static IResult BuildValidation(IReadOnlyList<ErrorBase> errors, int status, string operationId)
    {
        var map = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var group in errors.GroupBy(FieldKey))
        {
            map[group.Key] = group.Select(e => e.Message).ToArray();
        }

        var problem = new HttpValidationProblemDetails(map)
        {
            Type = "about:blank",
            Title = ReasonPhrase(status),
            Status = status,
        };
        problem.Extensions["code"] = errors[0].Code;
        problem.Extensions["errorType"] = ErrorType.Validation.ToString();
        problem.Extensions["operationId"] = operationId;

        return Results.Json<HttpValidationProblemDetails>(problem, statusCode: status, contentType: ProblemJson);
    }

    private static bool AllValidation(IReadOnlyList<ErrorBase> errors)
    {
        foreach (var e in errors)
        {
            if (e.Type != ErrorType.Validation)
            {
                return false;
            }
        }

        return true;
    }

    private static string FieldKey(ErrorBase error)
    {
        if (error.Metadata is { } meta && meta.TryGetValue("field", out var value) && value is string field)
        {
            return field;
        }

        return error.Code;
    }

    private static string ReasonPhrase(int status) => status switch
    {
        400 => "Bad Request",
        401 => "Unauthorized",
        403 => "Forbidden",
        404 => "Not Found",
        409 => "Conflict",
        422 => "Unprocessable Entity",
        500 => "Internal Server Error",
        _ => "Error",
    };

    private sealed record ErrorEntry(
        [property: JsonPropertyName("code")] string Code,
        [property: JsonPropertyName("message")] string Message);
}
```

Note: `ProblemDetails` lives in `Microsoft.AspNetCore.Mvc`; `HttpValidationProblemDetails` in `Microsoft.AspNetCore.Http`. Both ship in the shared framework. `ProblemDetails.Extensions` is `[JsonExtensionData]`, so `code`/`errorType`/`operationId`/`errors` flatten to the top level of the JSON.

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test tests/Vostra.Results.AspNetCore.Tests --filter ProblemResultBuilderTests`
Expected: PASS (all four tests).

- [ ] **Step 6: Commit**

```bash
git add src/Vostra.Results.AspNetCore/ProblemResultBuilder.cs tests/Vostra.Results.AspNetCore.Tests/HttpResultHarness.cs tests/Vostra.Results.AspNetCore.Tests/ProblemResultBuilderTests.cs
git commit -m "feat(aspnetcore): build RFC 7807 problem responses with code and errorType"
```

---

### Task 7: ToHttpResponse extension methods

**Files:**
- Create: `src/Vostra.Results.AspNetCore/ToHttpResponseExtensions.cs`
- Test: `tests/Vostra.Results.AspNetCore.Tests/ToHttpResponseTests.cs`

**Interfaces:**
- Consumes: `Result<T>`, `Result`, `SuccessKind`, `SuccessEnvelope<T>`, `ListEnvelope<T>`, `Pagination`, `ProblemResultBuilder`, `OperationId`, `VostraResultsOptions`.
- Produces:
  - `public static IResult ToHttpResponse<T>(this Result<T> result, HttpContext http)`
  - `public static IResult ToHttpResponse<T>(this Result<IEnumerable<T>> result, HttpContext http, Pagination pagination)`
  - `public static IResult ToHttpResponse(this Result result, HttpContext http)`
- Helper (private): reads `http.RequestServices.GetService<VostraResultsOptions>() ?? VostraResultsOptions.Default`; success status = `successKind == SuccessKind.Created ? 201 : 200`.

- [ ] **Step 1: Write the failing test**

`tests/Vostra.Results.AspNetCore.Tests/ToHttpResponseTests.cs`:

```csharp
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Vostra.Results.AspNetCore.Tests;

public class ToHttpResponseTests
{
    private static DefaultHttpContext Context(VostraResultsOptions? options = null)
    {
        var services = new ServiceCollection();
        if (options is not null)
        {
            services.AddSingleton(options);
        }

        return new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(),
            TraceIdentifier = "trace-xyz",
        };
    }

    [Fact]
    public async Task Ok_value_returns_200_success_envelope()
    {
        Result<int> result = 42;
        var res = await HttpResultHarness.Execute(result.ToHttpResponse(Context()), null);

        res.Status.Should().Be(200);
        res.Json.GetProperty("data").GetInt32().Should().Be(42);
        res.Json.GetProperty("operationId").GetString().Should().Be("trace-xyz");
    }

    [Fact]
    public async Task Created_value_returns_201()
    {
        var result = Result.Created(99);
        var ctx = Context();
        var res = await HttpResultHarness.Execute(result.ToHttpResponse(ctx), ctx.RequestServices);

        res.Status.Should().Be(201);
        res.Json.GetProperty("data").GetInt32().Should().Be(99);
    }

    [Fact]
    public async Task Error_value_returns_mapped_status_without_AddVostraResults()
    {
        Result<int> result = new NotFoundError("nope", code: "Thing.NotFound");
        var ctx = Context(); // no options registered -> default fallback
        var res = await HttpResultHarness.Execute(result.ToHttpResponse(ctx), ctx.RequestServices);

        res.Status.Should().Be(404);
        res.Json.GetProperty("code").GetString().Should().Be("Thing.NotFound");
    }

    [Fact]
    public async Task Registered_options_override_status()
    {
        var options = new VostraResultsOptions().MapStatus(ErrorType.Conflict, 422);
        var ctx = Context(options);
        Result<int> result = new ConflictError("dupe");
        var res = await HttpResultHarness.Execute(result.ToHttpResponse(ctx), ctx.RequestServices);

        res.Status.Should().Be(422);
    }

    [Fact]
    public async Task Collection_with_pagination_returns_list_envelope()
    {
        Result<IEnumerable<int>> result = Result.Ok<IEnumerable<int>>(new[] { 1, 2, 3 });
        var ctx = Context();
        var res = await HttpResultHarness.Execute(
            result.ToHttpResponse(ctx, new Pagination(1, 20, 3)), ctx.RequestServices);

        res.Status.Should().Be(200);
        res.Json.GetProperty("data").GetArrayLength().Should().Be(3);
        res.Json.GetProperty("pagination").GetProperty("totalCount").GetInt64().Should().Be(3);
    }

    [Fact]
    public async Task Non_generic_success_returns_operationId_only()
    {
        var result = Result.Success;
        var ctx = Context();
        var res = await HttpResultHarness.Execute(result.ToHttpResponse(ctx), ctx.RequestServices);

        res.Status.Should().Be(200);
        res.Json.GetProperty("operationId").GetString().Should().Be("trace-xyz");
        res.Json.TryGetProperty("data", out _).Should().BeFalse();
    }

    [Fact]
    public async Task Non_generic_failure_returns_problem_details()
    {
        var result = Result.Failure(new ValidationError("bad"));
        var ctx = Context();
        var res = await HttpResultHarness.Execute(result.ToHttpResponse(ctx), ctx.RequestServices);

        res.Status.Should().Be(400);
        res.Json.GetProperty("errorType").GetString().Should().Be("Validation");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Vostra.Results.AspNetCore.Tests --filter ToHttpResponseTests`
Expected: FAIL (`ToHttpResponse` not found).

- [ ] **Step 3: Implement the extensions**

`src/Vostra.Results.AspNetCore/ToHttpResponseExtensions.cs`:

```csharp
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Vostra.Results.AspNetCore;

/// <summary>Maps <see cref="Result"/>/<see cref="Result{T}"/> onto an HTTP <see cref="IResult"/>.</summary>
public static class ToHttpResponseExtensions
{
    /// <summary>Maps a <see cref="Result{T}"/> to a 200/201 success envelope or an RFC 7807 error.</summary>
    public static IResult ToHttpResponse<T>(this Result<T> result, HttpContext http)
    {
        var operationId = OperationId.For(http);
        if (result.TryGetValue(out var value))
        {
            var envelope = new SuccessEnvelope<T> { OperationId = operationId, Data = value };
            return Results.Json(envelope, statusCode: SuccessStatus(result.SuccessKind));
        }

        return ProblemResultBuilder.Build(result.Errors, OptionsFrom(http), operationId);
    }

    /// <summary>Maps a <see cref="Result{T}"/> of a sequence to a paginated list envelope or an RFC 7807 error.</summary>
    public static IResult ToHttpResponse<T>(this Result<IEnumerable<T>> result, HttpContext http, Pagination pagination)
    {
        var operationId = OperationId.For(http);
        if (result.TryGetValue(out var value))
        {
            var envelope = new ListEnvelope<T>
            {
                OperationId = operationId,
                Data = value.ToList(),
                Pagination = pagination,
            };
            return Results.Json(envelope, statusCode: SuccessStatus(result.SuccessKind));
        }

        return ProblemResultBuilder.Build(result.Errors, OptionsFrom(http), operationId);
    }

    /// <summary>Maps a non-generic <see cref="Result"/> to a 200/201 (operation id only) or an RFC 7807 error.</summary>
    public static IResult ToHttpResponse(this Result result, HttpContext http)
    {
        var operationId = OperationId.For(http);
        if (result.IsSuccess)
        {
            var envelope = new SuccessEnvelope<object?> { OperationId = operationId, Data = null };
            return Results.Json(envelope, statusCode: SuccessStatus(result.SuccessKind));
        }

        return ProblemResultBuilder.Build(result.Errors, OptionsFrom(http), operationId);
    }

    private static int SuccessStatus(SuccessKind kind) => kind == SuccessKind.Created ? 201 : 200;

    private static VostraResultsOptions OptionsFrom(HttpContext http) =>
        http.RequestServices?.GetService<VostraResultsOptions>() ?? VostraResultsOptions.Default;
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/Vostra.Results.AspNetCore.Tests --filter ToHttpResponseTests`
Expected: PASS (all seven tests).

- [ ] **Step 5: Run the whole suite**

Run: `dotnet test tests/Vostra.Results.AspNetCore.Tests`
Expected: PASS (all tasks' tests, ×2 frameworks).

- [ ] **Step 6: Commit**

```bash
git add src/Vostra.Results.AspNetCore/ToHttpResponseExtensions.cs tests/Vostra.Results.AspNetCore.Tests/ToHttpResponseTests.cs
git commit -m "feat(aspnetcore): add ToHttpResponse extensions for Result and Result<T>"
```

---

### Task 8: FR-11.3 round-trip readiness test + usage-style call-site test

**Files:**
- Test: `tests/Vostra.Results.AspNetCore.Tests/WireContractTests.cs`

**Interfaces:**
- Consumes: everything above. No production code — this task proves the wire contract is sufficient for the Testing package and that call-sites read naturally (acceptance §10.1, §10.4a).

- [ ] **Step 1: Write the round-trip + usage test**

`tests/Vostra.Results.AspNetCore.Tests/WireContractTests.cs`:

```csharp
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Vostra.Results.AspNetCore.Tests;

public class WireContractTests
{
    private static DefaultHttpContext Context() => new()
    {
        RequestServices = new ServiceCollection().BuildServiceProvider(),
        TraceIdentifier = "t-1",
    };

    // Simulates what the future Testing package will do: read status + code + errorType back off the wire.
    [Fact]
    public async Task Error_identity_survives_the_wire_for_typed_assertions()
    {
        Result<int> result = new NotFoundError("missing", code: "Order.NotFound");
        var res = await HttpResultHarness.Execute(result.ToHttpResponse(Context()), null);

        // The Testing package rebuilds (code, errorType) from these fields:
        var code = res.Json.GetProperty("code").GetString();
        var errorType = res.Json.GetProperty("errorType").GetString();

        res.Status.Should().Be(404);
        code.Should().Be("Order.NotFound");
        errorType.Should().Be(nameof(ErrorType.NotFound));
    }

    // Usage-first: a handler-style call site reads as a one-liner and produces the success envelope.
    [Fact]
    public async Task Handler_style_call_site_reads_naturally()
    {
        Result<string> Handle(string id) => id == "1" ? "found" : new NotFoundError($"id {id}");

        var ctx = Context();
        var ok = await HttpResultHarness.Execute(Handle("1").ToHttpResponse(ctx), ctx.RequestServices);
        var missing = await HttpResultHarness.Execute(Handle("2").ToHttpResponse(ctx), ctx.RequestServices);

        ok.Status.Should().Be(200);
        ok.Json.GetProperty("data").GetString().Should().Be("found");
        missing.Status.Should().Be(404);
    }
}
```

- [ ] **Step 2: Run the test**

Run: `dotnet test tests/Vostra.Results.AspNetCore.Tests --filter WireContractTests`
Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add tests/Vostra.Results.AspNetCore.Tests/WireContractTests.cs
git commit -m "test(aspnetcore): prove wire contract carries typed-error identity"
```

---

### Task 9: Documentation (package README + repo README section)

**Files:**
- Create: `src/Vostra.Results.AspNetCore/README.md`
- Modify: `README.md` (repo root — add an AspNetCore section; if the root README does not exist, create it with a short library overview + this section)

**Interfaces:** none (docs only). Must match the actual public surface shipped in Tasks 1–7.

- [ ] **Step 1: Write the package README**

`src/Vostra.Results.AspNetCore/README.md`:

````markdown
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
````

- [ ] **Step 2: Add the AspNetCore section to the repo-root README**

If `README.md` exists at the repo root, add this section after the Core description. If it does not exist,
create `README.md` with a one-paragraph overview of the `Vostra.Results` family and then this section:

```markdown
## Vostra.Results.AspNetCore

HTTP mapping for `Result`/`Result<T>`: `result.ToHttpResponse(HttpContext)` returns an `IResult` with a thin
success envelope or an RFC 7807 `ProblemDetails` error carrying `code` + `errorType`. Status comes from the
error via a DI-configured, open/closed map. See
[src/Vostra.Results.AspNetCore/README.md](src/Vostra.Results.AspNetCore/README.md).
```

- [ ] **Step 3: Verify docs match the shipped API**

Re-read the package README against `ToHttpResponseExtensions.cs`, `ServiceCollectionExtensions.cs`, and
`Envelopes.cs`. Confirm every type name, method name, and parameter (`Pagination(page, pageSize, totalCount)`,
`MapStatus`, `MapStatusForCode`, `ToHttpResponse`) matches exactly. Fix any drift.

- [ ] **Step 4: Build to confirm README packs (no broken csproj)**

Run: `dotnet build src/Vostra.Results.AspNetCore/Vostra.Results.AspNetCore.csproj`
Expected: build succeeds (the `PackageReadmeFile`/`None Include="README.md"` now resolves).

- [ ] **Step 5: Commit**

```bash
git add src/Vostra.Results.AspNetCore/README.md README.md
git commit -m "docs(aspnetcore): add package and repo README usage docs"
```

---

### Task 10: Final verification

**Files:** none (verification only).

- [ ] **Step 1: Full solution build + test**

Run: `dotnet test Vostra.Results.sln`
Expected: all Core tests (74) + all AspNetCore tests pass, on both net8.0 and net9.0, zero warnings (TreatWarningsAsErrors).

- [ ] **Step 2: Confirm dependency hygiene (NFR-1)**

Run: `dotnet list src/Vostra.Results.AspNetCore/Vostra.Results.AspNetCore.csproj package`
Expected: no transitive NuGet runtime packages beyond the framework reference; only the `Vostra.Results` project reference. (FrameworkReference does not appear as a package — that's expected.)

- [ ] **Step 3: Confirm Core stayed HTTP-free**

Run: `git diff --stat main -- src/Vostra.Results`
Expected: empty (no changes to Core).

- [ ] **Step 4: Update memory pointer**

Update `project-core-build-status` memory (or add a new `project-aspnetcore-build-status`) to record that AspNetCore is complete, and refresh `MEMORY.md`. (Do this per the memory instructions, not as a git commit.)

---

## Self-Review

**Spec coverage:**
- §2 package/deps → Task 1 (csproj, FrameworkReference, ProjectReference) + Task 10 (NFR-1 check). ✓
- §3 public surface (3 overloads, both call-site styles, RequestServices fallback) → Task 7 + Task 8 usage test. ✓
- §4 + §4.1 DI status mapping, precedence, default map → Tasks 3, 4. ✓
- §5.1 success envelope → Task 2 + Task 7. ✓
- §5.2 list envelope + Pagination → Task 2 + Task 7. ✓
- §5.3 error ProblemDetails (type/title/content-type/code/errorType/operationId) → Task 6. ✓
- §5.4 multi-error first-status + errors[] → Task 6. ✓
- §5.5 validation field map + key convention → Task 6. ✓
- §6 operationId → Task 5. ✓
- §7 shared contract (public DefaultStatusMap, code+errorType on wire) → Task 3 (public map) + Task 6 (wire) + Task 8 (round-trip proof). ✓
- §9 testing strategy (execute IResult vs DefaultHttpContext, both call-site styles, edge cases) → Tasks 2,3,6,7,8 + HttpResultHarness. ✓
- §9a documentation → Task 9. ✓
- §9b migration notes → Task 9 README. ✓
- §10 acceptance criteria 1–9 + 4a → covered across Tasks 6,7,8,10. ✓

**Placeholder scan:** No TBD/TODO; every code step shows complete code; every test shows real assertions. ✓

**Type consistency:** `ToHttpResponse` / `AddVostraResults` / `MapStatus` / `MapStatusForCode` / `Pagination(Page,PageSize,TotalCount)` / `SuccessEnvelope<T>.Data` / `ListEnvelope<T>` / `ProblemResultBuilder.Build(IReadOnlyList<ErrorBase>, VostraResultsOptions, string)` / `OperationId.For(HttpContext)` / `ErrorStatusResolver.Resolve(ErrorBase, VostraResultsOptions)` / `DefaultStatusMap.ForType(ErrorType)` are used identically across tasks. Core members used (`TryGetValue`, `SuccessKind`, `Errors`, `Result.Created`, `Result.Ok<T>`, `Result.Success`, `Result.Failure`) all exist in the current Core surface. ✓
