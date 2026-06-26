# Transport-Neutral Testing Split — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Split `Vostra.Result.Testing` into a Core-only chain-and-assert package and a new `Vostra.Result.AspNetCore.Testing` HTTP-adapter package, so the test-composition layer works over any `Task<Result<T>>` with no ASP.NET Core dependency.

**Architecture:** Two steps. First, an in-place behavior-preserving change: neutralize `RequestContext` to `(Operation, Target, Body)` and add a public `WithRequestContext` attach helper. Then the structural split: move the five HTTP-adapter files into a new package, repoint the neutral package to Core only, and split the test project in two (the neutral test project references only the neutral package, structurally proving no AspNetCore leak).

**Tech Stack:** C# (`net8.0` + `net9.0`), xUnit, FluentAssertions. No new dependencies.

**Spec:** [docs/superpowers/specs/2026-06-22-transport-neutral-testing-design.md](../specs/2026-06-22-transport-neutral-testing-design.md)

## Global Constraints

- **0-warning bar**; `dotnet test` green on **net8.0 + net9.0**.
- Commit messages contain **NO Claude/Anthropic attribution** (per CLAUDE.md).
- **Neutral package (`Vostra.Result.Testing`) and the neutral test project must reference ONLY `Vostra.Result`** (Core) — no AspNetCore, no FrameworkReference. This is the acceptance proof.
- **Quirk:** if `dotnet test`/`dotnet sln` says "Unable to find a project to restore" or projects vanish, run `git restore Vostra.Result.sln`. Prefer `dotnet sln add` over hand-editing the `.sln`.
- Branch: `feature/maximo-feedback-transport-neutral` (already checked out).
- Behavior of every moved/renamed type is unchanged — this is a packaging + visibility change plus one additive helper.

---

### Task 1: Neutralize `RequestContext` + add the attach helper (in place)

**Files:**
- Modify: `src/Vostra.Result.Testing/RequestContext.cs`
- Create: `src/Vostra.Result.Testing/RequestContextExtensions.cs`
- Modify: `src/Vostra.Result.Testing/ResultAssertions.cs:133-141` (Describe)
- Modify: `src/Vostra.Result.Testing/TestHttpClient.cs:117-135` (AttachRequest)
- Test: `tests/Vostra.Result.Testing.Tests/ResultAssertionsTests.cs` (append)

**Interfaces:**
- Produces:
  ```csharp
  public sealed record RequestContext(string Operation, string Target, object? Body);   // Vostra.Result.Testing
  public static class RequestContextExtensions                                            // Vostra.Result.Testing
  {
      public const string RequestMetadataKey = "request";
      public static ErrorBase WithRequestContext(this ErrorBase error, RequestContext context);
  }
  ```

- [ ] **Step 1: Write the failing test** — append to `tests/Vostra.Result.Testing.Tests/ResultAssertionsTests.cs`:

```csharp
[Fact]
public void WithRequestContext_attaches_and_Describe_renders_neutral_fields()
{
    ErrorBase error = new NotFoundError("missing", "Thing.NotFound")
        .WithRequestContext(new RequestContext("SEND", "wo-inbound", "payload"));

    Result<int> result = error;

    var ex = Assert.Throws<VostraAssertionException>(() => result.ShouldBeSuccess());
    ex.Message.Should().Contain("SEND wo-inbound");
    ex.Message.Should().Contain("body: payload");
}
```

- [ ] **Step 2: Run it, verify it fails to compile**

Run: `dotnet test --filter "FullyQualifiedName~WithRequestContext_attaches" 2>&1 | tail -n 15`
Expected: FAIL — `WithRequestContext` not defined (and, after Step 3 partial work, field-name mismatches).

- [ ] **Step 3a: Neutralize `RequestContext`** — replace the body of `src/Vostra.Result.Testing/RequestContext.cs`:

```csharp
namespace Vostra.Result.Testing;

/// <summary>Describes the operation that produced a failed result — attached to the failing error so
/// assertion failure messages can show what was attempted. Diagnostic only; carries no behavior.</summary>
/// <param name="Operation">What was attempted — an HTTP verb ("GET"), a queue action ("SEND"), etc.</param>
/// <param name="Target">Where — a request URL, a queue/topic name, an endpoint.</param>
/// <param name="Body">The payload sent, if any.</param>
public sealed record RequestContext(string Operation, string Target, object? Body);
```

- [ ] **Step 3b: Add the attach helper** — create `src/Vostra.Result.Testing/RequestContextExtensions.cs`:

```csharp
using Vostra.Result;

namespace Vostra.Result.Testing;

/// <summary>Attaching diagnostic <see cref="RequestContext"/> to an error, for any transport.</summary>
public static class RequestContextExtensions
{
    /// <summary>Metadata key under which a <see cref="RequestContext"/> is attached to an error.</summary>
    public const string RequestMetadataKey = "request";

    /// <summary>Returns a copy of <paramref name="error"/> with <paramref name="context"/> attached for
    /// diagnostics, preserving any existing metadata and the concrete error type.</summary>
    public static ErrorBase WithRequestContext(this ErrorBase error, RequestContext context)
    {
        var metadata = error.Metadata is null
            ? new Dictionary<string, object?>()
            : new Dictionary<string, object?>(error.Metadata);
        metadata[RequestMetadataKey] = context;
        return error.WithMetadata(metadata);
    }
}
```

- [ ] **Step 3c: Update `Describe`** — in `src/Vostra.Result.Testing/ResultAssertions.cs`, replace the request-rendering block (currently lines 133-141):

```csharp
            if (error.Metadata is { } metadata
                && metadata.TryGetValue(RequestContextExtensions.RequestMetadataKey, out var raw)
                && raw is RequestContext request)
            {
                builder.Append(Environment.NewLine).Append("    request: ")
                    .Append(request.Operation).Append(' ').Append(request.Target);
                if (request.Body is not null)
                {
                    builder.Append(" | body: ").Append(request.Body);
                }
            }
```

- [ ] **Step 3d: Route `TestHttpClient` through the helper** — in `src/Vostra.Result.Testing/TestHttpClient.cs`, replace the body of `AttachRequest` from the `var metadata = ...` line through `return array;` (currently lines 123-134) with:

```csharp
        array[0] = array[0].WithRequestContext(new RequestContext(method.Method, Combine(url), body));
        return array;
```

(The helper now does the metadata merge that this block did by hand.)

- [ ] **Step 4: Run the new test + full suite, verify green**

Run: `dotnet test 2>&1 | tail -n 20`
Expected: PASS — all existing tests + the new one, both TFMs, 0 warnings. (Existing HTTP tests still pass: the rendered line is unchanged for HTTP because `method.Method`→`Operation`, url→`Target` are positional.)

- [ ] **Step 5: Commit**

```bash
git add src/Vostra.Result.Testing/RequestContext.cs src/Vostra.Result.Testing/RequestContextExtensions.cs src/Vostra.Result.Testing/ResultAssertions.cs src/Vostra.Result.Testing/TestHttpClient.cs tests/Vostra.Result.Testing.Tests/ResultAssertionsTests.cs
git commit -m "refactor(testing): neutralize RequestContext to (Operation, Target, Body) + add WithRequestContext

Diagnostic context is now transport-neutral with a public attach helper and
metadata-key const; TestHttpClient routes through it. Behavior unchanged."
```

---

### Task 2: Execute the package split (src + tests + solution)

This is one atomic structural move — moving source files breaks compilation until the test project is repointed, so it lands green as a unit.

**Files:**
- Create: `src/Vostra.Result.AspNetCore.Testing/Vostra.Result.AspNetCore.Testing.csproj`
- Move (git mv): `TestHttpClient.cs`, `ProblemDetailsErrorReader.cs`, `RawJsonFormat.cs`, `IResultRawFormat.cs`, `PagedList.cs` → `src/Vostra.Result.AspNetCore.Testing/`
- Modify: `src/Vostra.Result.Testing/Vostra.Result.Testing.csproj` (→ Core-only)
- Create: `tests/Vostra.Result.AspNetCore.Testing.Tests/Vostra.Result.AspNetCore.Testing.Tests.csproj` + `GlobalUsings.cs`
- Move (git mv): `PagedListTests.cs`, `ProblemDetailsErrorReaderTests.cs`, `RawJsonFormatTests.cs`, `TestHttpClientTests.cs`, `FakeRawFormat.cs`, `StubHttpMessageHandler.cs` → `tests/Vostra.Result.AspNetCore.Testing.Tests/`
- Modify: `tests/Vostra.Result.Testing.Tests/Vostra.Result.Testing.Tests.csproj` (→ Core-only) + `GlobalUsings.cs`
- Modify: `Vostra.Result.sln`

**Interfaces:**
- Consumes: the neutral types from Task 1 (`RequestContext`, `RequestContextExtensions`).
- Produces: namespace `Vostra.Result.AspNetCore.Testing` for the five moved HTTP files; package `Vostra.Result.AspNetCore.Testing` referencing `Vostra.Result.AspNetCore` + `Vostra.Result.Testing`.

- [ ] **Step 1: Create the new HTTP-adapter project** — `src/Vostra.Result.AspNetCore.Testing/Vostra.Result.AspNetCore.Testing.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>net8.0;net9.0</TargetFrameworks>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <PackageId>Vostra.Result.AspNetCore.Testing</PackageId>
    <Description>HTTP integration-testing toolkit for Vostra.Result — a TestHttpClient returning Result&lt;T&gt; with typed-error reconstruction over the Vostra.Result.AspNetCore response contract.</Description>
    <PackageReadmeFile>README.md</PackageReadmeFile>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\Vostra.Result.AspNetCore\Vostra.Result.AspNetCore.csproj" />
    <ProjectReference Include="..\Vostra.Result.Testing\Vostra.Result.Testing.csproj" />
  </ItemGroup>

  <ItemGroup>
    <None Include="README.md" Pack="true" PackagePath="\" Condition="Exists('README.md')" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Move the five HTTP source files**

```bash
git mv src/Vostra.Result.Testing/TestHttpClient.cs src/Vostra.Result.Testing/ProblemDetailsErrorReader.cs src/Vostra.Result.Testing/RawJsonFormat.cs src/Vostra.Result.Testing/IResultRawFormat.cs src/Vostra.Result.Testing/PagedList.cs src/Vostra.Result.AspNetCore.Testing/
```

- [ ] **Step 3: Change the namespace + add neutral using in the moved files**

In **all five** moved files, change `namespace Vostra.Result.Testing;` → `namespace Vostra.Result.AspNetCore.Testing;`.

In `src/Vostra.Result.AspNetCore.Testing/TestHttpClient.cs` only, add to the using block at the top (it now references the neutral `RequestContext`/`WithRequestContext` across packages):

```csharp
using Vostra.Result.Testing;
```

- [ ] **Step 4: Repoint the neutral package to Core-only** — replace `src/Vostra.Result.Testing/Vostra.Result.Testing.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>net8.0;net9.0</TargetFrameworks>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <PackageId>Vostra.Result.Testing</PackageId>
    <Description>Transport-neutral testing toolkit for Vostra.Result — fluent chain-and-assert helpers over any Task&lt;Result&lt;T&gt;&gt; with rich diagnostics. Depends only on the core type.</Description>
    <PackageReadmeFile>README.md</PackageReadmeFile>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\Vostra.Result\Vostra.Result.csproj" />
  </ItemGroup>

  <ItemGroup>
    <None Include="README.md" Pack="true" PackagePath="\" Condition="Exists('README.md')" />
  </ItemGroup>
</Project>
```

- [ ] **Step 5: Create the new HTTP test project** — `tests/Vostra.Result.AspNetCore.Testing.Tests/Vostra.Result.AspNetCore.Testing.Tests.csproj`:

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
    <PackageReference Include="Microsoft.AspNetCore.TestHost" Version="8.0.0" Condition="'$(TargetFramework)' == 'net8.0'" />
    <PackageReference Include="Microsoft.AspNetCore.TestHost" Version="9.0.0" Condition="'$(TargetFramework)' == 'net9.0'" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="xunit" Version="2.5.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.3" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\Vostra.Result.AspNetCore.Testing\Vostra.Result.AspNetCore.Testing.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 6: Create the HTTP test global usings** — `tests/Vostra.Result.AspNetCore.Testing.Tests/GlobalUsings.cs`:

```csharp
global using FluentAssertions;
global using Xunit;
global using Vostra.Result;
global using Vostra.Result.AspNetCore;
global using Vostra.Result.Testing;
global using Vostra.Result.AspNetCore.Testing;
```

- [ ] **Step 7: Move the six HTTP test files**

```bash
git mv tests/Vostra.Result.Testing.Tests/PagedListTests.cs tests/Vostra.Result.Testing.Tests/ProblemDetailsErrorReaderTests.cs tests/Vostra.Result.Testing.Tests/RawJsonFormatTests.cs tests/Vostra.Result.Testing.Tests/TestHttpClientTests.cs tests/Vostra.Result.Testing.Tests/FakeRawFormat.cs tests/Vostra.Result.Testing.Tests/StubHttpMessageHandler.cs tests/Vostra.Result.AspNetCore.Testing.Tests/
```

- [ ] **Step 8: Make the neutral test project Core-only** — replace `tests/Vostra.Result.Testing.Tests/Vostra.Result.Testing.Tests.csproj`:

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
    <PackageReference Include="coverlet.collector" Version="6.0.0" />
    <PackageReference Include="FluentAssertions" Version="8.10.0" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="xunit" Version="2.5.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.3" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\Vostra.Result.Testing\Vostra.Result.Testing.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 9: Trim the neutral test global usings** — replace `tests/Vostra.Result.Testing.Tests/GlobalUsings.cs`:

```csharp
global using FluentAssertions;
global using Xunit;
global using Vostra.Result;
global using Vostra.Result.Testing;
```

- [ ] **Step 10: Register both new projects in the solution**

```bash
dotnet sln Vostra.Result.sln add src/Vostra.Result.AspNetCore.Testing/Vostra.Result.AspNetCore.Testing.csproj tests/Vostra.Result.AspNetCore.Testing.Tests/Vostra.Result.AspNetCore.Testing.Tests.csproj
```

- [ ] **Step 11: Build the whole solution and run all tests**

Run: `dotnet test 2>&1 | tail -n 30`
Expected: PASS on net8.0 + net9.0, 0 warnings. Test counts unchanged in total (the HTTP tests now run under the new project). If "Unable to find a project to restore", run `git restore Vostra.Result.sln` and re-add via Step 10.

- [ ] **Step 12: Prove the neutral package has no AspNetCore dependency**

Run: `dotnet build src/Vostra.Result.Testing/Vostra.Result.Testing.csproj 2>&1 | tail -n 5` and confirm it builds. Then verify neither the neutral src nor neutral test project mentions AspNetCore:

Run: `grep -rn "AspNetCore" src/Vostra.Result.Testing/ tests/Vostra.Result.Testing.Tests/`
Expected: **no matches** (empty output). This is the acceptance proof. If anything matches, that type/using is mis-placed — move it to the HTTP side until the grep is clean.

- [ ] **Step 13: Commit**

```bash
git add -A
git commit -m "refactor(testing): split into Vostra.Result.Testing (Core-only) + Vostra.Result.AspNetCore.Testing

The chain-and-assert layer now depends only on the core type; the TestHttpClient
verbs + RFC 7807 reconstruction move to a dedicated HTTP-adapter package. Test
project split so the neutral suite references only Core, proving no HTTP leak."
```

---

### Task 3: Documentation

**Files:**
- Modify: `docs/usage.md` (§3, ~lines 263-363)
- Rewrite: `src/Vostra.Result.Testing/README.md`; Create: `src/Vostra.Result.AspNetCore.Testing/README.md`
- Modify: `CLAUDE.md` (Status / package list)
- Modify: `cc/plans/2026-06-22-nuget-and-prerelease-hardening.md` (publish order + NFR-1 note)

- [ ] **Step 1: Rework `docs/usage.md` §3** — change the section heading and opening so the transport-neutral layer leads, then present the HTTP client as one adapter. Replace the `## 3. Vostra.Result.Testing — HTTP → Result` heading and its intro paragraph with:

```markdown
## 3. Vostra.Result.Testing — chain & assert over any `Task<Result<T>>`

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
// assertion failure renders:  request: SEND wo-inbound | body: WorkOrder { ... }
```

### HTTP adapter — `Vostra.Result.AspNetCore.Testing`

For ASP.NET Core APIs, the `Vostra.Result.AspNetCore.Testing` package adds `TestHttpClient`, which turns
the HTTP round-trip back into a `Result<T>` (reconstructing the typed error from the RFC 7807 body) so the
same chain-and-assert layer reads as a domain script over your endpoints:
```

(Keep the existing `TestHttpClient` examples that follow — they now live under this HTTP-adapter subsection. Update the install line near the top of the file to list both packages: `Vostra.Result.Testing` and `Vostra.Result.AspNetCore.Testing`.)

- [ ] **Step 2: Rewrite the neutral README** — replace `src/Vostra.Result.Testing/README.md` with a short intro describing the transport-neutral chain-and-assert layer (mirror §3 above): what it is, the `Then`/`Assert`/`ShouldBe…` surface, `WithRequestContext`, and that it depends only on `Vostra.Result`. Point HTTP users to `Vostra.Result.AspNetCore.Testing`.

- [ ] **Step 3: Create the HTTP-adapter README** — create `src/Vostra.Result.AspNetCore.Testing/README.md` describing `TestHttpClient`, typed-error reconstruction from RFC 7807, lists/pagination, the in-process test-server flow, and the injectable `IResultRawFormat`. (Lift the relevant prose from the old Testing README content.)

- [ ] **Step 4: Update CLAUDE.md** — in the Status section, change the Testing bullet to describe the two packages: `Vostra.Result.Testing` (transport-neutral chain+assert, Core-only) and `Vostra.Result.AspNetCore.Testing` (HTTP `TestHttpClient`). Note the dependency change (Testing no longer depends on AspNetCore).

- [ ] **Step 5: Update the hardening plan** — in `cc/plans/2026-06-22-nuget-and-prerelease-hardening.md`, update the publish-order note: Core → {AspNetCore, Testing}; AspNetCore → AspNetCore.Testing (four packages now; Testing publishes off Core alone). Reflect that the NFR-1 "Testing depends on AspNetCore" rationale now applies to `Vostra.Result.AspNetCore.Testing`, not `Vostra.Result.Testing`.

- [ ] **Step 6: Sanity build + commit**

Run: `dotnet build 2>&1 | tail -n 5` (docs don't affect compilation, but confirm nothing else drifted).

```bash
git add docs/usage.md src/Vostra.Result.Testing/README.md src/Vostra.Result.AspNetCore.Testing/README.md CLAUDE.md cc/plans/2026-06-22-nuget-and-prerelease-hardening.md
git commit -m "docs: lead with transport-neutral testing; document the AspNetCore.Testing HTTP adapter"
```

---

## Self-Review

**Spec coverage:**
- Two-package split + dependency direction (spec §2) → Task 2. ✓
- File moves, verified list (§2.1) → Task 2 Steps 2-3. ✓
- Namespaces (§2.2) → Task 2 Step 3 + global usings (Steps 6, 9). ✓
- `RequestContext` neutralized, request-only (§3.1) → Task 1 Step 3a. ✓
- Public attach path: const + `WithRequestContext`, shared by HTTP (§3.2) → Task 1 Steps 3b-3d. ✓
- Test split + structural proof (§4) → Task 2 Steps 5-9, 12. ✓
- Out of scope: no messaging adapter, no ErrorType change (§5) → nothing implements them. ✓
- Docs: usage §3, READMEs, CLAUDE.md, hardening plan (§6) → Task 3. ✓
- Acceptance: neutral-only compile + grep proof, HTTP behavior unchanged, diagnostics render renamed fields (§7) → Task 1 Step 4, Task 2 Steps 11-12. ✓

**Placeholder scan:** No TBD/TODO; every code/edit step shows complete content. README prose steps (Task 3 Steps 2-3) describe content rather than dictating exact wording — acceptable for docs, and §3 of usage shows the concrete shape to mirror.

**Type consistency:** `RequestContext(Operation, Target, Body)`, `RequestContextExtensions.RequestMetadataKey`, and `WithRequestContext(this ErrorBase, RequestContext)` are used identically across spec, Task 1 implementation, `Describe`, `TestHttpClient`, and the usage doc. Package/namespace `Vostra.Result.AspNetCore.Testing` consistent across both new `.csproj`s, the moved files, and global usings.
