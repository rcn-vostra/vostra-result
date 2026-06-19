# Vostra.Results Core v1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the dependency-free `Vostra.Results` Core package — a `readonly struct Result<T>` with a typed `Error` hierarchy, implicit conversions, `Match`/`Switch`, a uniform sync+async combinator matrix, LINQ query syntax, and aggregation.

**Architecture:** A `readonly struct Result<T>` (and non-generic `Result`) holds either a value or one-or-more `Error`s, discriminated by an `_initialized` flag so `default(Result<T>)` is a *faulted* result, not a silent success. Value access is only via `Match`/`Switch`/`TryGet` — there is no public `Value`. Sync combinators are instance methods on the struct; the async matrix is extension methods on `Task<Result<T>>` that await and delegate, so any async step "lifts" a chain to `Task<Result<>>` and it stays there. The neutral `ErrorType` enum carries failure taxonomy only — no HTTP.

**Tech Stack:** C# (LangVersion latest), .NET `net8.0;net9.0`, BCL only for the library. Tests: xUnit + FluentAssertions.

## Global Constraints

- Namespace: `Vostra.Results` (library), `Vostra.Results.Tests` (tests).
- Target frameworks: `net8.0;net9.0`.
- Library project references **only the BCL** — zero NuGet runtime dependencies (NFR-1).
- `Nullable` enabled; `LangVersion` latest; `GenerateDocumentationFile` true; `TreatWarningsAsErrors` true.
- No public `Value` property on `Result<T>` (FR-1.3). Value access only via `Match`/`Switch`/`MatchFirst`/`SwitchFirst`/`TryGetValue`/`GetValueOr`.
- `default(Result<T>)` and `default(Result)` MUST be faulted (yield a synthetic `UnexpectedError`), never a success.
- Success path allocates nothing; only the failure path allocates (NFR-2).
- Tests are written as **usage scenarios** (call-site shapes), not internal-state pokes.
- Commit after every task with a conventional-commit message.
- Commit message footer line: `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`

---

### Task 1: Repository scaffolding

**Files:**
- Create: `Directory.Build.props`
- Create: `Vostra.Results.sln`
- Create: `src/Vostra.Results/Vostra.Results.csproj`
- Create: `tests/Vostra.Results.Tests/Vostra.Results.Tests.csproj`
- Create: `LICENSE`
- Create: `THIRD-PARTY-NOTICES.md`
- Create: `.gitignore`
- Test: `tests/Vostra.Results.Tests/SmokeTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: a buildable solution with a passing test project; the `Vostra.Results` namespace root.

- [ ] **Step 1: Create `.gitignore`**

```gitignore
bin/
obj/
*.user
.vs/
artifacts/
```

- [ ] **Step 2: Create `Directory.Build.props`**

```xml
<Project>
  <PropertyGroup>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <Authors>Vostra</Authors>
    <Company>Vostra</Company>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <RepositoryType>git</RepositoryType>
  </PropertyGroup>
</Project>
```

- [ ] **Step 3: Create the projects and solution**

Run:
```bash
dotnet new sln -n Vostra.Results
dotnet new classlib -o src/Vostra.Results -f net8.0
dotnet new xunit -o tests/Vostra.Results.Tests -f net8.0
rm src/Vostra.Results/Class1.cs tests/Vostra.Results.Tests/UnitTest1.cs
dotnet sln add src/Vostra.Results/Vostra.Results.csproj
dotnet sln add tests/Vostra.Results.Tests/Vostra.Results.Tests.csproj
dotnet add tests/Vostra.Results.Tests reference src/Vostra.Results/Vostra.Results.csproj
dotnet add tests/Vostra.Results.Tests package FluentAssertions
```

- [ ] **Step 4: Set multi-targeting and metadata in `src/Vostra.Results/Vostra.Results.csproj`**

Replace the file contents with:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>net8.0;net9.0</TargetFrameworks>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <PackageId>Vostra.Results</PackageId>
    <Description>A lean, async-friendly, dependency-free Result&lt;T&gt; type for .NET.</Description>
  </PropertyGroup>
</Project>
```

- [ ] **Step 5: Multi-target the test project `tests/Vostra.Results.Tests/Vostra.Results.Tests.csproj`**

Ensure the `<TargetFramework>net8.0</TargetFramework>` line reads:
```xml
<TargetFrameworks>net8.0;net9.0</TargetFrameworks>
```
(rename the element from singular `TargetFramework` to plural `TargetFrameworks`).

- [ ] **Step 6: Create `LICENSE` (MIT)**

```text
MIT License

Copyright (c) 2026 Vostra

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

- [ ] **Step 7: Create `THIRD-PARTY-NOTICES.md`**

```markdown
# Third-Party Notices

Vostra.Results borrows design ideas (and, where noted in source, small amounts of code)
from the following MIT-licensed projects. Their copyright notices are reproduced here.

## ErrorOr — Copyright (c) Amichai Mantinband (MIT)
Implicit value/error conversions, `Match`/`Switch`, `Then` async chaining, `ErrorType` + code.

## FluentResults — Copyright (c) Michael Altmann (MIT)
Typed errors, `CausedBy(exception)`, metadata, `Merge`/aggregation.

## OneOf — Copyright (c) 2016 Harry McIntyre (MIT)
Discriminated-union / exhaustive match ergonomics (reference for later work).
```

- [ ] **Step 8: Write the smoke test `tests/Vostra.Results.Tests/SmokeTests.cs`**

```csharp
namespace Vostra.Results.Tests;

public class SmokeTests
{
    [Fact]
    public void Solution_builds_and_tests_run()
    {
        true.Should().BeTrue();
    }
}
```

- [ ] **Step 9: Add a global using for FluentAssertions `tests/Vostra.Results.Tests/GlobalUsings.cs`**

```csharp
global using FluentAssertions;
global using Xunit;
```

- [ ] **Step 10: Build and test**

Run: `dotnet test`
Expected: build succeeds for both TFMs; 1 test passes (×2 frameworks).

- [ ] **Step 11: Commit**

```bash
git add -A
git commit -m "chore: scaffold Vostra.Results solution, Core + test projects"
```

---

### Task 2: Error model — `ErrorType`, abstract `Error`, built-in kinds

**Files:**
- Create: `src/Vostra.Results/ErrorType.cs`
- Create: `src/Vostra.Results/Error.cs`
- Create: `src/Vostra.Results/Errors.cs`
- Test: `tests/Vostra.Results.Tests/ErrorTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `enum ErrorType { Failure, Unexpected, Validation, NotFound, Conflict, Unauthorized, Forbidden }`
  - `abstract class Error` with `string Code`, `string Message`, `ErrorType Type`, `Exception? CausedBy`, `IReadOnlyDictionary<string,object?>? Metadata`; value equality on `(GetType, Type, Code, Message)`.
  - Sealed subclasses: `ValidationError`, `NotFoundError`, `ConflictError`, `AlreadyExistsError`, `UnauthorizedError`, `ForbiddenError`, `UnexpectedError`. Each ctor: `(string message, string code = <default>, Exception? causedBy = null, IReadOnlyDictionary<string,object?>? metadata = null)`.

- [ ] **Step 1: Write the failing tests `tests/Vostra.Results.Tests/ErrorTests.cs`**

```csharp
namespace Vostra.Results.Tests;

public class ErrorTests
{
    [Fact]
    public void Builtin_error_carries_type_and_default_code()
    {
        var error = new NotFoundError("Order 5 not found");

        error.Type.Should().Be(ErrorType.NotFound);
        error.Code.Should().Be("General.NotFound");
        error.Message.Should().Be("Order 5 not found");
        error.CausedBy.Should().BeNull();
        error.Metadata.Should().BeNull();
    }

    [Fact]
    public void Errors_of_same_type_code_and_message_are_equal()
    {
        Error a = new NotFoundError("missing", code: "Order.NotFound");
        Error b = new NotFoundError("missing", code: "Order.NotFound");

        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void Errors_of_different_subtype_are_not_equal()
    {
        Error a = new NotFoundError("x", code: "Same");
        Error b = new ConflictError("x", code: "Same");

        a.Should().NotBe(b);
    }

    [Fact]
    public void CausedBy_and_metadata_are_opt_in()
    {
        var ex = new InvalidOperationException("boom");
        var meta = new Dictionary<string, object?> { ["orderId"] = 5 };

        var error = new UnexpectedError("failed", causedBy: ex, metadata: meta);

        error.CausedBy.Should().BeSameAs(ex);
        error.Metadata.Should().ContainKey("orderId");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~ErrorTests"`
Expected: FAIL — `ErrorType`, `NotFoundError`, etc. do not exist (compile error).

- [ ] **Step 3: Create `src/Vostra.Results/ErrorType.cs`**

```csharp
namespace Vostra.Results;

/// <summary>
/// Transport-neutral taxonomy of failure kinds. Carries no HTTP semantics —
/// status mapping lives in the Vostra.Results.AspNetCore package.
/// </summary>
public enum ErrorType
{
    /// <summary>A generic failure.</summary>
    Failure,

    /// <summary>An unexpected fault (e.g. wrapping an exception).</summary>
    Unexpected,

    /// <summary>Input failed validation.</summary>
    Validation,

    /// <summary>A requested resource was not found.</summary>
    NotFound,

    /// <summary>The request conflicts with current state.</summary>
    Conflict,

    /// <summary>Authentication is required or failed.</summary>
    Unauthorized,

    /// <summary>The caller is authenticated but not permitted.</summary>
    Forbidden,
}
```

- [ ] **Step 4: Create `src/Vostra.Results/Error.cs`**

```csharp
namespace Vostra.Results;

/// <summary>
/// Base type for all errors. An error carries a stable <see cref="Code"/> identity,
/// a human-readable <see cref="Message"/>, a neutral <see cref="Type"/>, and optional
/// <see cref="CausedBy"/> / <see cref="Metadata"/>. Extend by subclassing.
/// </summary>
public abstract class Error : IEquatable<Error>
{
    /// <summary>Initializes a new <see cref="Error"/>.</summary>
    protected Error(
        string code,
        string message,
        ErrorType type,
        Exception? causedBy = null,
        IReadOnlyDictionary<string, object?>? metadata = null)
    {
        Code = code;
        Message = message;
        Type = type;
        CausedBy = causedBy;
        Metadata = metadata;
    }

    /// <summary>Stable, machine-readable identity, e.g. "Order.NotFound".</summary>
    public string Code { get; }

    /// <summary>Human-readable description.</summary>
    public string Message { get; }

    /// <summary>Neutral failure taxonomy.</summary>
    public ErrorType Type { get; }

    /// <summary>Optional originating exception (opt-in).</summary>
    public Exception? CausedBy { get; }

    /// <summary>Optional metadata bag (opt-in).</summary>
    public IReadOnlyDictionary<string, object?>? Metadata { get; }

    /// <summary>Value equality on concrete type, <see cref="Type"/>, <see cref="Code"/>, and <see cref="Message"/>.</summary>
    public bool Equals(Error? other) =>
        other is not null
        && GetType() == other.GetType()
        && Type == other.Type
        && Code == other.Code
        && Message == other.Message;

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as Error);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(GetType(), Type, Code, Message);

    /// <inheritdoc />
    public override string ToString() => $"{GetType().Name}({Code}: {Message})";
}
```

- [ ] **Step 5: Create `src/Vostra.Results/Errors.cs`**

```csharp
namespace Vostra.Results;

/// <summary>Input failed validation (maps to 400 in the HTTP layer).</summary>
public sealed class ValidationError : Error
{
    /// <summary>Creates a <see cref="ValidationError"/>.</summary>
    public ValidationError(
        string message,
        string code = "General.Validation",
        Exception? causedBy = null,
        IReadOnlyDictionary<string, object?>? metadata = null)
        : base(code, message, ErrorType.Validation, causedBy, metadata) { }
}

/// <summary>A requested resource was not found (maps to 404).</summary>
public sealed class NotFoundError : Error
{
    /// <summary>Creates a <see cref="NotFoundError"/>.</summary>
    public NotFoundError(
        string message,
        string code = "General.NotFound",
        Exception? causedBy = null,
        IReadOnlyDictionary<string, object?>? metadata = null)
        : base(code, message, ErrorType.NotFound, causedBy, metadata) { }
}

/// <summary>The request conflicts with current state (maps to 409).</summary>
public sealed class ConflictError : Error
{
    /// <summary>Creates a <see cref="ConflictError"/>.</summary>
    public ConflictError(
        string message,
        string code = "General.Conflict",
        Exception? causedBy = null,
        IReadOnlyDictionary<string, object?>? metadata = null)
        : base(code, message, ErrorType.Conflict, causedBy, metadata) { }
}

/// <summary>A resource already exists (a conflict; maps to 409).</summary>
public sealed class AlreadyExistsError : Error
{
    /// <summary>Creates an <see cref="AlreadyExistsError"/>.</summary>
    public AlreadyExistsError(
        string message,
        string code = "General.AlreadyExists",
        Exception? causedBy = null,
        IReadOnlyDictionary<string, object?>? metadata = null)
        : base(code, message, ErrorType.Conflict, causedBy, metadata) { }
}

/// <summary>Authentication is required or failed (maps to 401).</summary>
public sealed class UnauthorizedError : Error
{
    /// <summary>Creates an <see cref="UnauthorizedError"/>.</summary>
    public UnauthorizedError(
        string message,
        string code = "General.Unauthorized",
        Exception? causedBy = null,
        IReadOnlyDictionary<string, object?>? metadata = null)
        : base(code, message, ErrorType.Unauthorized, causedBy, metadata) { }
}

/// <summary>The caller is authenticated but not permitted (maps to 403).</summary>
public sealed class ForbiddenError : Error
{
    /// <summary>Creates a <see cref="ForbiddenError"/>.</summary>
    public ForbiddenError(
        string message,
        string code = "General.Forbidden",
        Exception? causedBy = null,
        IReadOnlyDictionary<string, object?>? metadata = null)
        : base(code, message, ErrorType.Forbidden, causedBy, metadata) { }
}

/// <summary>An unexpected fault (maps to 500). Often wraps an exception via <see cref="Error.CausedBy"/>.</summary>
public sealed class UnexpectedError : Error
{
    /// <summary>Creates an <see cref="UnexpectedError"/>.</summary>
    public UnexpectedError(
        string message,
        string code = "General.Unexpected",
        Exception? causedBy = null,
        IReadOnlyDictionary<string, object?>? metadata = null)
        : base(code, message, ErrorType.Unexpected, causedBy, metadata) { }
}
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~ErrorTests"`
Expected: PASS (all 4, ×2 frameworks).

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat: add ErrorType taxonomy and typed Error hierarchy"
```

---

### Task 3: `Result<T>` core — state, conversions, default-is-faulted, equality

**Files:**
- Create: `src/Vostra.Results/ResultSentinels.cs`
- Create: `src/Vostra.Results/ResultOfT.cs`
- Test: `tests/Vostra.Results.Tests/ResultOfTTests.cs`

**Interfaces:**
- Consumes: `Error`, `ErrorType`, `UnexpectedError`.
- Produces:
  - `readonly struct Result<T> : IEquatable<Result<T>>`
  - `bool IsSuccess`, `bool IsError`, `IReadOnlyList<Error> Errors`, `Error FirstError`
  - implicit operators from `T`, `Error`, `Error[]`, `List<Error>`
  - internal factories `Result<T>.Ok(T value)`, `Result<T>.Err(Error[] errors)`
  - internal `T UnsafeValue`, internal `Error[] ErrorArray`, internal `Result<U> ToError<U>()`
  - value equality, `==`/`!=`, `ToString()`

- [ ] **Step 1: Write the failing tests `tests/Vostra.Results.Tests/ResultOfTTests.cs`**

```csharp
namespace Vostra.Results.Tests;

public class ResultOfTTests
{
    private static Result<int> Produce(bool ok) =>
        ok ? 42 : new NotFoundError("missing");

    [Fact]
    public void Value_converts_implicitly_to_success()
    {
        Result<int> result = 42;

        result.IsSuccess.Should().BeTrue();
        result.IsError.Should().BeFalse();
    }

    [Fact]
    public void Error_converts_implicitly_to_failure()
    {
        Result<int> result = new NotFoundError("missing");

        result.IsError.Should().BeTrue();
        result.FirstError.Should().BeOfType<NotFoundError>();
        result.Errors.Should().ContainSingle();
    }

    [Fact]
    public void Default_result_is_faulted_not_a_success()
    {
        Result<int> result = default;

        result.IsError.Should().BeTrue();
        result.IsSuccess.Should().BeFalse();
        result.FirstError.Should().BeOfType<UnexpectedError>();
        result.FirstError.Code.Should().Be("Result.Uninitialized");
    }

    [Fact]
    public void Successes_with_equal_values_are_equal()
    {
        Result<int> a = 7;
        Result<int> b = 7;

        a.Should().Be(b);
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void Failures_with_equal_errors_are_equal()
    {
        Result<int> a = new NotFoundError("x", code: "C");
        Result<int> b = new NotFoundError("x", code: "C");

        a.Should().Be(b);
    }

    [Fact]
    public void ToString_distinguishes_success_and_error()
    {
        Produce(true).ToString().Should().Contain("Success");
        Produce(false).ToString().Should().Contain("Error");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~ResultOfTTests"`
Expected: FAIL — `Result<T>` does not exist (compile error).

- [ ] **Step 3: Create `src/Vostra.Results/ResultSentinels.cs`**

```csharp
namespace Vostra.Results;

/// <summary>Shared singletons so the uninitialized-result error allocates once, not per generic instantiation.</summary>
internal static class ResultSentinels
{
    internal static readonly Error Uninitialized =
        new UnexpectedError(
            "A Result was used while uninitialized (default(Result)).",
            code: "Result.Uninitialized");

    internal static readonly Error[] UninitializedArray = { Uninitialized };

    internal static readonly IReadOnlyList<Error> UninitializedList = UninitializedArray;
}
```

- [ ] **Step 4: Create `src/Vostra.Results/ResultOfT.cs`**

```csharp
using System.Diagnostics.CodeAnalysis;

namespace Vostra.Results;

/// <summary>
/// Carries either a value of type <typeparamref name="T"/> or one-or-more <see cref="Error"/>s.
/// The success path does not allocate. There is no public Value — consume via
/// <see cref="Match{TOut}"/>, <see cref="Switch"/>, or the TryGet members.
/// </summary>
public readonly struct Result<T> : IEquatable<Result<T>>
{
    private readonly T? _value;
    private readonly Error[]? _errors;
    private readonly bool _initialized;

    private Result(bool initialized, T? value, Error[]? errors)
    {
        _initialized = initialized;
        _value = value;
        _errors = errors;
    }

    internal static Result<T> Ok(T value) => new(initialized: true, value, errors: null);

    internal static Result<T> Err(Error[] errors) =>
        new(initialized: true, value: default,
            errors: errors is { Length: > 0 } ? errors : ResultSentinels.UninitializedArray);

    /// <summary>True when this result carries a value.</summary>
    public bool IsSuccess => !IsError;

    /// <summary>True when this result carries error(s), including the uninitialized <c>default</c>.</summary>
    public bool IsError => !_initialized || _errors is not null;

    /// <summary>The error(s); empty when this is a success.</summary>
    public IReadOnlyList<Error> Errors =>
        _errors ?? (_initialized ? Array.Empty<Error>() : ResultSentinels.UninitializedList);

    /// <summary>The first error. Throws if this is a success.</summary>
    public Error FirstError =>
        IsError ? Errors[0] : throw new InvalidOperationException("Result is a success; there is no error.");

    internal T UnsafeValue => _value!;

    internal Error[] ErrorArray => _errors ?? ResultSentinels.UninitializedArray;

    /// <summary>Re-tags this failure as a <c>Result&lt;U&gt;</c>, preserving the errors.</summary>
    internal Result<U> ToError<U>() => Result<U>.Err(ErrorArray);

    /// <summary>Creates a success from a value.</summary>
    public static implicit operator Result<T>(T value) => Ok(value);

    /// <summary>Creates a failure from a single error.</summary>
    public static implicit operator Result<T>(Error error) => Err(new[] { error });

    /// <summary>Creates a failure from an array of errors.</summary>
    public static implicit operator Result<T>(Error[] errors) => Err(errors);

    /// <summary>Creates a failure from a list of errors.</summary>
    public static implicit operator Result<T>(List<Error> errors) => Err(errors.ToArray());

    /// <inheritdoc />
    public bool Equals(Result<T> other)
    {
        if (IsError != other.IsError)
        {
            return false;
        }

        return IsError
            ? Errors.SequenceEqual(other.Errors)
            : EqualityComparer<T>.Default.Equals(_value, other._value);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Result<T> other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() =>
        IsError
            ? Errors.Aggregate(17, (hash, error) => HashCode.Combine(hash, error))
            : EqualityComparer<T>.Default.GetHashCode(_value!);

    /// <summary>Value equality.</summary>
    public static bool operator ==(Result<T> left, Result<T> right) => left.Equals(right);

    /// <summary>Value inequality.</summary>
    public static bool operator !=(Result<T> left, Result<T> right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString() =>
        IsError ? $"Error[{Errors.Count}]({FirstError})" : $"Success({_value})";
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~ResultOfTTests"`
Expected: PASS (all 6, ×2 frameworks).

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat: add Result<T> struct with implicit conversions and faulted default"
```

---

### Task 4: Non-generic `Result` + factory entry points

**Files:**
- Create: `src/Vostra.Results/Result.cs`
- Test: `tests/Vostra.Results.Tests/ResultTests.cs`

**Interfaces:**
- Consumes: `Error`, `Result<T>`, `ResultSentinels`.
- Produces:
  - `readonly struct Result : IEquatable<Result>` with `IsSuccess`/`IsError`/`Errors`/`FirstError`
  - `static Result Success { get; }`
  - `static Result Failure(Error error)`
  - `static Result<T> Ok<T>(T value)`, `static Result<T> Fail<T>(Error error)`
  - internal `static Result FromErrors(Error[] errors)`
  - implicit operators from `Error`, `Error[]`
  - equality, `ToString()`

- [ ] **Step 1: Write the failing tests `tests/Vostra.Results.Tests/ResultTests.cs`**

```csharp
namespace Vostra.Results.Tests;

public class ResultTests
{
    [Fact]
    public void Success_is_a_success()
    {
        Result.Success.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Failure_carries_the_error()
    {
        Result result = Result.Failure(new ConflictError("dup"));

        result.IsError.Should().BeTrue();
        result.FirstError.Should().BeOfType<ConflictError>();
    }

    [Fact]
    public void Default_nongeneric_result_is_faulted()
    {
        Result result = default;

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Result.Uninitialized");
    }

    [Fact]
    public void Ok_factory_builds_a_generic_success()
    {
        Result<string> result = Result.Ok("hi");

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Fail_factory_builds_a_generic_failure()
    {
        Result<string> result = Result.Fail<string>(new NotFoundError("nope"));

        result.IsError.Should().BeTrue();
        result.FirstError.Should().BeOfType<NotFoundError>();
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~ResultTests"`
Expected: FAIL — `Result` (non-generic) does not exist.

- [ ] **Step 3: Create `src/Vostra.Results/Result.cs`**

```csharp
namespace Vostra.Results;

/// <summary>
/// A success/failure result without a value, for void-returning operations.
/// Also the single discoverable entry point for explicit factories.
/// </summary>
public readonly struct Result : IEquatable<Result>
{
    private readonly Error[]? _errors;
    private readonly bool _initialized;

    private Result(bool initialized, Error[]? errors)
    {
        _initialized = initialized;
        _errors = errors;
    }

    /// <summary>A successful (valueless) result.</summary>
    public static Result Success { get; } = new(initialized: true, errors: null);

    internal static Result FromErrors(Error[] errors) =>
        new(initialized: true, errors: errors is { Length: > 0 } ? errors : ResultSentinels.UninitializedArray);

    /// <summary>Creates a failed result from a single error.</summary>
    public static Result Failure(Error error) => FromErrors(new[] { error });

    /// <summary>Creates a successful <c>Result&lt;T&gt;</c> from a value.</summary>
    public static Result<T> Ok<T>(T value) => value;

    /// <summary>Creates a failed <c>Result&lt;T&gt;</c> from an error.</summary>
    public static Result<T> Fail<T>(Error error) => error;

    /// <summary>True when this result is a success.</summary>
    public bool IsSuccess => !IsError;

    /// <summary>True when this result carries error(s), including the uninitialized <c>default</c>.</summary>
    public bool IsError => !_initialized || _errors is not null;

    /// <summary>The error(s); empty when this is a success.</summary>
    public IReadOnlyList<Error> Errors =>
        _errors ?? (_initialized ? Array.Empty<Error>() : ResultSentinels.UninitializedList);

    /// <summary>The first error. Throws if this is a success.</summary>
    public Error FirstError =>
        IsError ? Errors[0] : throw new InvalidOperationException("Result is a success; there is no error.");

    /// <summary>Creates a failed result from a single error.</summary>
    public static implicit operator Result(Error error) => Failure(error);

    /// <summary>Creates a failed result from an array of errors.</summary>
    public static implicit operator Result(Error[] errors) => FromErrors(errors);

    /// <inheritdoc />
    public bool Equals(Result other)
    {
        if (IsError != other.IsError)
        {
            return false;
        }

        return !IsError || Errors.SequenceEqual(other.Errors);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Result other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() =>
        IsError ? Errors.Aggregate(17, (hash, error) => HashCode.Combine(hash, error)) : 0;

    /// <summary>Value equality.</summary>
    public static bool operator ==(Result left, Result right) => left.Equals(right);

    /// <summary>Value inequality.</summary>
    public static bool operator !=(Result left, Result right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString() => IsError ? $"Error[{Errors.Count}]({FirstError})" : "Success";
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~ResultTests"`
Expected: PASS (all 5, ×2 frameworks).

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: add non-generic Result and factory entry points"
```

---

### Task 5: Inspection & extraction — `Match`, `Switch`, `TryGet`, `GetValueOr`

**Files:**
- Create: `src/Vostra.Results/ResultOfT.Inspection.cs`
- Test: `tests/Vostra.Results.Tests/InspectionTests.cs`

**Interfaces:**
- Consumes: `Result<T>` (internal `UnsafeValue`), `Error`.
- Produces (instance members on `Result<T>`):
  - `TOut Match<TOut>(Func<T,TOut> onOk, Func<IReadOnlyList<Error>,TOut> onErr)`
  - `TOut MatchFirst<TOut>(Func<T,TOut> onOk, Func<Error,TOut> onFirstError)`
  - `void Switch(Action<T> onOk, Action<IReadOnlyList<Error>> onErr)`
  - `void SwitchFirst(Action<T> onOk, Action<Error> onFirstError)`
  - `bool TryGetValue(out T value)`
  - `bool TryGetErrors(out IReadOnlyList<Error>? errors)`
  - `T GetValueOr(T fallback)` and `T GetValueOr(Func<IReadOnlyList<Error>,T> fallback)`

- [ ] **Step 1: Write the failing tests `tests/Vostra.Results.Tests/InspectionTests.cs`**

```csharp
namespace Vostra.Results.Tests;

public class InspectionTests
{
    private static Result<int> Find(int id) =>
        id == 1 ? 100 : new NotFoundError($"id {id}");

    [Fact]
    public void Match_runs_the_success_branch()
    {
        var label = Find(1).Match(
            onOk: v => $"value {v}",
            onErr: errors => errors[0].Message);

        label.Should().Be("value 100");
    }

    [Fact]
    public void Match_runs_the_error_branch_without_touching_value()
    {
        var label = Find(2).Match(
            onOk: v => $"value {v}",
            onErr: errors => errors[0].Message);

        label.Should().Be("id 2");
    }

    [Fact]
    public void MatchFirst_passes_the_first_error()
    {
        var type = Find(2).MatchFirst(_ => ErrorType.Failure, e => e.Type);

        type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public void Switch_invokes_the_matching_action()
    {
        var seen = "";
        Find(1).Switch(v => seen = $"ok{v}", _ => seen = "err");
        seen.Should().Be("ok100");
    }

    [Fact]
    public void TryGetValue_is_true_on_success()
    {
        Find(1).TryGetValue(out var value).Should().BeTrue();
        value.Should().Be(100);
    }

    [Fact]
    public void TryGetValue_is_false_on_error()
    {
        Find(2).TryGetValue(out _).Should().BeFalse();
    }

    [Fact]
    public void TryGetErrors_is_true_on_error()
    {
        Find(2).TryGetErrors(out var errors).Should().BeTrue();
        errors!.Should().ContainSingle();
    }

    [Fact]
    public void GetValueOr_returns_fallback_on_error()
    {
        Find(2).GetValueOr(-1).Should().Be(-1);
        Find(1).GetValueOr(-1).Should().Be(100);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~InspectionTests"`
Expected: FAIL — `Match`/`Switch`/`TryGetValue`/etc. do not exist.

- [ ] **Step 3: Create `src/Vostra.Results/ResultOfT.Inspection.cs`**

```csharp
using System.Diagnostics.CodeAnalysis;

namespace Vostra.Results;

public readonly partial struct Result<T>
{
    /// <summary>Runs <paramref name="onOk"/> on the value or <paramref name="onErr"/> on the errors.</summary>
    public TOut Match<TOut>(Func<T, TOut> onOk, Func<IReadOnlyList<Error>, TOut> onErr) =>
        IsError ? onErr(Errors) : onOk(UnsafeValue);

    /// <summary>Like <see cref="Match{TOut}"/>, but the error branch receives only the first error.</summary>
    public TOut MatchFirst<TOut>(Func<T, TOut> onOk, Func<Error, TOut> onFirstError) =>
        IsError ? onFirstError(FirstError) : onOk(UnsafeValue);

    /// <summary>Runs the matching action.</summary>
    public void Switch(Action<T> onOk, Action<IReadOnlyList<Error>> onErr)
    {
        if (IsError)
        {
            onErr(Errors);
        }
        else
        {
            onOk(UnsafeValue);
        }
    }

    /// <summary>Like <see cref="Switch"/>, but the error branch receives only the first error.</summary>
    public void SwitchFirst(Action<T> onOk, Action<Error> onFirstError)
    {
        if (IsError)
        {
            onFirstError(FirstError);
        }
        else
        {
            onOk(UnsafeValue);
        }
    }

    /// <summary>Gets the value when this is a success.</summary>
    public bool TryGetValue([MaybeNullWhen(false)] out T value)
    {
        if (IsSuccess)
        {
            value = UnsafeValue;
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>Gets the errors when this is a failure.</summary>
    public bool TryGetErrors([NotNullWhen(true)] out IReadOnlyList<Error>? errors)
    {
        if (IsError)
        {
            errors = Errors;
            return true;
        }

        errors = null;
        return false;
    }

    /// <summary>Returns the value, or <paramref name="fallback"/> when this is a failure.</summary>
    public T GetValueOr(T fallback) => IsSuccess ? UnsafeValue : fallback;

    /// <summary>Returns the value, or the result of <paramref name="fallback"/> when this is a failure.</summary>
    public T GetValueOr(Func<IReadOnlyList<Error>, T> fallback) => IsSuccess ? UnsafeValue : fallback(Errors);
}
```

- [ ] **Step 4: Make `Result<T>` partial**

In `src/Vostra.Results/ResultOfT.cs`, change the declaration line:
```csharp
public readonly struct Result<T> : IEquatable<Result<T>>
```
to:
```csharp
public readonly partial struct Result<T> : IEquatable<Result<T>>
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~InspectionTests"`
Expected: PASS (all 9, ×2 frameworks).

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat: add Match/Switch and safe extraction to Result<T>"
```

---

### Task 6: Synchronous combinators — `Map`, `Then`, `Tap`, `TapError`, `Ensure`, `MapError`

**Files:**
- Create: `src/Vostra.Results/ResultOfT.Combinators.cs`
- Test: `tests/Vostra.Results.Tests/SyncCombinatorTests.cs`

**Interfaces:**
- Consumes: `Result<T>` (internal `UnsafeValue`, `ToError<U>`, `ErrorArray`), `Error`, `ValidationError`.
- Produces (instance members on `Result<T>`):
  - `Result<U> Map<U>(Func<T,U> map)`
  - `Result<U> Then<U>(Func<T,Result<U>> next)`
  - `Result<T> Tap(Action<T> action)`
  - `Result<T> TapError(Action<IReadOnlyList<Error>> action)`
  - `Result<T> Ensure(Func<T,bool> predicate, Error error)`
  - `Result<T> Ensure(Func<T,bool> predicate, string validationMessage)` (defaults to `ValidationError`)
  - `Result<T> MapError(Func<Error,Error> map)`

- [ ] **Step 1: Write the failing tests `tests/Vostra.Results.Tests/SyncCombinatorTests.cs`**

```csharp
namespace Vostra.Results.Tests;

public class SyncCombinatorTests
{
    private static Result<int> Ok(int v) => v;
    private static Result<int> Err() => new NotFoundError("missing");

    [Fact]
    public void Map_transforms_success()
    {
        Ok(2).Map(v => v * 10).Should().Be(Result.Ok(20));
    }

    [Fact]
    public void Map_propagates_error()
    {
        Err().Map(v => v * 10).IsError.Should().BeTrue();
    }

    [Fact]
    public void Then_chains_success()
    {
        Result<int> Half(int v) => v % 2 == 0 ? v / 2 : new ValidationError("odd");

        Ok(8).Then(Half).Should().Be(Result.Ok(4));
        Ok(7).Then(Half).FirstError.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public void Tap_runs_only_on_success_and_passes_through()
    {
        var seen = 0;
        var result = Ok(5).Tap(v => seen = v);

        seen.Should().Be(5);
        result.Should().Be(Result.Ok(5));

        seen = 0;
        Err().Tap(v => seen = v);
        seen.Should().Be(0);
    }

    [Fact]
    public void TapError_runs_only_on_error()
    {
        var count = 0;
        Err().TapError(_ => count++);
        Ok(1).TapError(_ => count++);
        count.Should().Be(1);
    }

    [Fact]
    public void Ensure_with_string_defaults_to_validation_error()
    {
        var result = Ok(3).Ensure(v => v > 10, "too small");

        result.IsError.Should().BeTrue();
        result.FirstError.Should().BeOfType<ValidationError>();
        result.FirstError.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public void Ensure_passes_through_when_predicate_holds()
    {
        Ok(20).Ensure(v => v > 10, "too small").Should().Be(Result.Ok(20));
    }

    [Fact]
    public void MapError_transforms_each_error()
    {
        var result = Err().MapError(e => new ValidationError(e.Message, code: "remapped"));

        result.FirstError.Should().BeOfType<ValidationError>();
        result.FirstError.Code.Should().Be("remapped");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~SyncCombinatorTests"`
Expected: FAIL — `Map`/`Then`/etc. do not exist.

- [ ] **Step 3: Create `src/Vostra.Results/ResultOfT.Combinators.cs`**

```csharp
namespace Vostra.Results;

public readonly partial struct Result<T>
{
    /// <summary>Maps the value with <paramref name="map"/> on success; propagates errors otherwise.</summary>
    public Result<U> Map<U>(Func<T, U> map) =>
        IsError ? ToError<U>() : Result<U>.Ok(map(UnsafeValue));

    /// <summary>Binds to another result with <paramref name="next"/> on success; propagates errors otherwise.</summary>
    public Result<U> Then<U>(Func<T, Result<U>> next) =>
        IsError ? ToError<U>() : next(UnsafeValue);

    /// <summary>Runs <paramref name="action"/> on success and returns this result unchanged.</summary>
    public Result<T> Tap(Action<T> action)
    {
        if (IsSuccess)
        {
            action(UnsafeValue);
        }

        return this;
    }

    /// <summary>Runs <paramref name="action"/> on failure and returns this result unchanged.</summary>
    public Result<T> TapError(Action<IReadOnlyList<Error>> action)
    {
        if (IsError)
        {
            action(Errors);
        }

        return this;
    }

    /// <summary>Fails with <paramref name="error"/> when <paramref name="predicate"/> is false on a success.</summary>
    public Result<T> Ensure(Func<T, bool> predicate, Error error) =>
        IsError ? this : (predicate(UnsafeValue) ? this : Result<T>.Err(new[] { error }));

    /// <summary>Fails with a <see cref="ValidationError"/> when <paramref name="predicate"/> is false (FR-5.4).</summary>
    public Result<T> Ensure(Func<T, bool> predicate, string validationMessage) =>
        Ensure(predicate, new ValidationError(validationMessage));

    /// <summary>Transforms each error with <paramref name="map"/>; passes successes through.</summary>
    public Result<T> MapError(Func<Error, Error> map) =>
        IsSuccess ? this : Result<T>.Err(Array.ConvertAll(ErrorArray, e => map(e)));
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~SyncCombinatorTests"`
Expected: PASS (all 8, ×2 frameworks).

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: add synchronous combinators to Result<T>"
```

---

### Task 7: Async matrix — `Task<Result<T>>` receiver and `Task` projections

**Files:**
- Create: `src/Vostra.Results/ResultOfT.AsyncProjections.cs` (sync receiver, async projection — instance methods)
- Create: `src/Vostra.Results/ResultAsyncExtensions.cs` (`Task<Result<T>>` receiver — extension methods)
- Test: `tests/Vostra.Results.Tests/AsyncMatrixTests.cs`

**Interfaces:**
- Consumes: `Result<T>` and all sync combinators from Task 6.
- Produces:
  - Instance on `Result<T>` (sync receiver, async projection), each returning `Task<Result<…>>`:
    `Map<U>(Func<T,Task<U>>)`, `Then<U>(Func<T,Task<Result<U>>>)`, `Tap(Func<T,Task>)`, `TapError(Func<IReadOnlyList<Error>,Task>)`, `Ensure(Func<T,Task<bool>>, Error)`, `Ensure(Func<T,Task<bool>>, string)`.
  - Extensions on `Task<Result<T>>` (async receiver), one per combinator × {sync projection, async projection}, all returning `Task<Result<…>>`: `Map`, `Then`, `Tap`, `TapError`, `Ensure` (Error + string), `MapError`.

- [ ] **Step 1: Write the failing tests `tests/Vostra.Results.Tests/AsyncMatrixTests.cs`**

```csharp
namespace Vostra.Results.Tests;

public class AsyncMatrixTests
{
    private static Task<Result<int>> GetAsync(int v) => Task.FromResult<Result<int>>(v);
    private static Task<Result<int>> DoubleAsync(int v) => Task.FromResult<Result<int>>(v * 2);

    [Fact]
    public async Task Mixed_sync_and_async_chain_uses_one_leading_await()
    {
        Result<string> result = await GetAsync(5)            // Task<Result<int>>
            .Ensure(v => v > 0, "must be positive")           // sync guard, mid-async
            .Then(v => DoubleAsync(v))                         // async T -> Task<Result>
            .Map(v => v + 1)                                   // sync map
            .Map(v => $"={v}");                                // sync map

        result.IsSuccess.Should().BeTrue();
        result.Match(s => s, _ => "err").Should().Be("=11");
    }

    [Fact]
    public async Task Error_short_circuits_the_async_chain()
    {
        Result<int> result = await GetAsync(-1)
            .Ensure(v => v > 0, "must be positive")
            .Then(v => DoubleAsync(v));

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task Async_projection_on_sync_receiver_lifts_to_task()
    {
        Result<int> seed = 4;
        Result<int> result = await seed.Then(v => DoubleAsync(v));
        result.Should().Be(Result.Ok(8));
    }

    [Fact]
    public async Task TapError_async_runs_on_failure()
    {
        var count = 0;
        Result<int> err = new NotFoundError("x");

        await Task.FromResult(err).TapError(_ => { count++; return Task.CompletedTask; });

        count.Should().Be(1);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~AsyncMatrixTests"`
Expected: FAIL — async overloads do not exist.

- [ ] **Step 3: Create `src/Vostra.Results/ResultOfT.AsyncProjections.cs`** (sync receiver, async projection)

```csharp
namespace Vostra.Results;

public readonly partial struct Result<T>
{
    /// <summary>Async <see cref="Map{U}(Func{T,U})"/>.</summary>
    public async Task<Result<U>> Map<U>(Func<T, Task<U>> map) =>
        IsError ? ToError<U>() : Result<U>.Ok(await map(UnsafeValue).ConfigureAwait(false));

    /// <summary>Async <see cref="Then{U}(Func{T,Result{U}})"/>.</summary>
    public Task<Result<U>> Then<U>(Func<T, Task<Result<U>>> next) =>
        IsError ? Task.FromResult(ToError<U>()) : next(UnsafeValue);

    /// <summary>Async <see cref="Tap(Action{T})"/>.</summary>
    public async Task<Result<T>> Tap(Func<T, Task> action)
    {
        if (IsSuccess)
        {
            await action(UnsafeValue).ConfigureAwait(false);
        }

        return this;
    }

    /// <summary>Async <see cref="TapError(Action{IReadOnlyList{Error}})"/>.</summary>
    public async Task<Result<T>> TapError(Func<IReadOnlyList<Error>, Task> action)
    {
        if (IsError)
        {
            await action(Errors).ConfigureAwait(false);
        }

        return this;
    }

    /// <summary>Async <see cref="Ensure(Func{T,bool},Error)"/>.</summary>
    public async Task<Result<T>> Ensure(Func<T, Task<bool>> predicate, Error error)
    {
        if (IsError)
        {
            return this;
        }

        return await predicate(UnsafeValue).ConfigureAwait(false) ? this : Result<T>.Err(new[] { error });
    }

    /// <summary>Async <see cref="Ensure(Func{T,bool},string)"/> defaulting to a <see cref="ValidationError"/>.</summary>
    public Task<Result<T>> Ensure(Func<T, Task<bool>> predicate, string validationMessage) =>
        Ensure(predicate, new ValidationError(validationMessage));
}
```

- [ ] **Step 4: Create `src/Vostra.Results/ResultAsyncExtensions.cs`** (`Task<Result<T>>` receiver)

```csharp
namespace Vostra.Results;

/// <summary>
/// Async-receiver overloads. Each awaits the source result then delegates to the matching
/// instance combinator, so a chain "lifts" to Task&lt;Result&lt;&gt;&gt; on its first async step
/// and stays there — one leading await, sync and async steps interleave freely.
/// </summary>
public static class ResultAsyncExtensions
{
    // ---- Map ----
    /// <summary>Async-receiver, sync projection.</summary>
    public static async Task<Result<U>> Map<T, U>(this Task<Result<T>> source, Func<T, U> map) =>
        (await source.ConfigureAwait(false)).Map(map);

    /// <summary>Async-receiver, async projection.</summary>
    public static async Task<Result<U>> Map<T, U>(this Task<Result<T>> source, Func<T, Task<U>> map) =>
        await (await source.ConfigureAwait(false)).Map(map).ConfigureAwait(false);

    // ---- Then ----
    /// <summary>Async-receiver, sync projection.</summary>
    public static async Task<Result<U>> Then<T, U>(this Task<Result<T>> source, Func<T, Result<U>> next) =>
        (await source.ConfigureAwait(false)).Then(next);

    /// <summary>Async-receiver, async projection.</summary>
    public static async Task<Result<U>> Then<T, U>(this Task<Result<T>> source, Func<T, Task<Result<U>>> next) =>
        await (await source.ConfigureAwait(false)).Then(next).ConfigureAwait(false);

    // ---- Tap ----
    /// <summary>Async-receiver, sync side-effect.</summary>
    public static async Task<Result<T>> Tap<T>(this Task<Result<T>> source, Action<T> action) =>
        (await source.ConfigureAwait(false)).Tap(action);

    /// <summary>Async-receiver, async side-effect.</summary>
    public static async Task<Result<T>> Tap<T>(this Task<Result<T>> source, Func<T, Task> action) =>
        await (await source.ConfigureAwait(false)).Tap(action).ConfigureAwait(false);

    // ---- TapError ----
    /// <summary>Async-receiver, sync side-effect.</summary>
    public static async Task<Result<T>> TapError<T>(this Task<Result<T>> source, Action<IReadOnlyList<Error>> action) =>
        (await source.ConfigureAwait(false)).TapError(action);

    /// <summary>Async-receiver, async side-effect.</summary>
    public static async Task<Result<T>> TapError<T>(this Task<Result<T>> source, Func<IReadOnlyList<Error>, Task> action) =>
        await (await source.ConfigureAwait(false)).TapError(action).ConfigureAwait(false);

    // ---- Ensure ----
    /// <summary>Async-receiver, sync predicate, explicit error.</summary>
    public static async Task<Result<T>> Ensure<T>(this Task<Result<T>> source, Func<T, bool> predicate, Error error) =>
        (await source.ConfigureAwait(false)).Ensure(predicate, error);

    /// <summary>Async-receiver, sync predicate, validation message.</summary>
    public static async Task<Result<T>> Ensure<T>(this Task<Result<T>> source, Func<T, bool> predicate, string validationMessage) =>
        (await source.ConfigureAwait(false)).Ensure(predicate, validationMessage);

    /// <summary>Async-receiver, async predicate, explicit error.</summary>
    public static async Task<Result<T>> Ensure<T>(this Task<Result<T>> source, Func<T, Task<bool>> predicate, Error error) =>
        await (await source.ConfigureAwait(false)).Ensure(predicate, error).ConfigureAwait(false);

    /// <summary>Async-receiver, async predicate, validation message.</summary>
    public static async Task<Result<T>> Ensure<T>(this Task<Result<T>> source, Func<T, Task<bool>> predicate, string validationMessage) =>
        await (await source.ConfigureAwait(false)).Ensure(predicate, validationMessage).ConfigureAwait(false);

    // ---- MapError ----
    /// <summary>Async-receiver error transform.</summary>
    public static async Task<Result<T>> MapError<T>(this Task<Result<T>> source, Func<Error, Error> map) =>
        (await source.ConfigureAwait(false)).MapError(map);
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~AsyncMatrixTests"`
Expected: PASS (all 4, ×2 frameworks).

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat: add Task-based async combinator matrix"
```

---

### Task 8: LINQ query syntax — `Select`, `SelectMany`, `Where`

**Files:**
- Create: `src/Vostra.Results/ResultOfT.Linq.cs` (sync, instance methods)
- Create: `src/Vostra.Results/ResultLinqAsyncExtensions.cs` (async-receiver `SelectMany`)
- Test: `tests/Vostra.Results.Tests/LinqTests.cs`

**Interfaces:**
- Consumes: `Result<T>` (`Map`, `Ensure`, `ToError`, `UnsafeValue`, `IsError`).
- Produces:
  - Instance on `Result<T>`:
    `Result<TResult> Select<TResult>(Func<T,TResult> selector)`,
    `Result<TResult> SelectMany<TMid,TResult>(Func<T,Result<TMid>> bind, Func<T,TMid,TResult> project)`,
    `Result<T> Where(Func<T,bool> predicate)`.
  - Extension on `Task<Result<T>>`:
    `Task<Result<TResult>> SelectMany<T,TMid,TResult>(this Task<Result<T>> source, Func<T,Task<Result<TMid>>> bind, Func<T,TMid,TResult> project)`.

- [ ] **Step 1: Write the failing tests `tests/Vostra.Results.Tests/LinqTests.cs`**

```csharp
namespace Vostra.Results.Tests;

public class LinqTests
{
    private static Result<int> GetOrder(int id) => id > 0 ? id : new NotFoundError("no order");
    private static Result<int> GetPayment(int orderId) => orderId * 10;
    private static Task<Result<int>> GetOrderAsync(int id) => Task.FromResult(GetOrder(id));
    private static Task<Result<int>> GetPaymentAsync(int orderId) => Task.FromResult(GetPayment(orderId));

    [Fact]
    public void Select_projects_success()
    {
        Result<int> q = from o in GetOrder(2) select o + 1;
        q.Should().Be(Result.Ok(3));
    }

    [Fact]
    public void SelectMany_composes_two_results()
    {
        Result<int> total =
            from o in GetOrder(2)
            from p in GetPayment(o)
            select o + p;

        total.Should().Be(Result.Ok(2 + 20));
    }

    [Fact]
    public void SelectMany_short_circuits_on_first_error()
    {
        Result<int> total =
            from o in GetOrder(-1)
            from p in GetPayment(o)
            select o + p;

        total.IsError.Should().BeTrue();
        total.FirstError.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public void Where_failure_is_a_validation_error()
    {
        Result<int> q = from o in GetOrder(2) where o > 100 select o;

        q.IsError.Should().BeTrue();
        q.FirstError.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task SelectMany_works_over_task_of_result()
    {
        Result<int> total = await (
            from o in GetOrderAsync(2)
            from p in GetPaymentAsync(o)
            select o + p);

        total.Should().Be(Result.Ok(2 + 20));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~LinqTests"`
Expected: FAIL — query pattern members do not exist.

- [ ] **Step 3: Create `src/Vostra.Results/ResultOfT.Linq.cs`**

```csharp
namespace Vostra.Results;

public readonly partial struct Result<T>
{
    /// <summary>LINQ <c>select</c>. Equivalent to <see cref="Map{U}(Func{T,U})"/>.</summary>
    public Result<TResult> Select<TResult>(Func<T, TResult> selector) => Map(selector);

    /// <summary>LINQ <c>from … from …</c>. Binds then projects; short-circuits on the first error.</summary>
    public Result<TResult> SelectMany<TMid, TResult>(
        Func<T, Result<TMid>> bind,
        Func<T, TMid, TResult> project)
    {
        if (IsError)
        {
            return ToError<TResult>();
        }

        var value = UnsafeValue;
        return bind(value).Map(mid => project(value, mid));
    }

    /// <summary>LINQ <c>where</c>. A false predicate yields a <see cref="ValidationError"/>.</summary>
    public Result<T> Where(Func<T, bool> predicate) =>
        Ensure(predicate, new ValidationError("The 'where' predicate was not satisfied.", code: "General.Validation"));
}
```

- [ ] **Step 4: Create `src/Vostra.Results/ResultLinqAsyncExtensions.cs`**

```csharp
namespace Vostra.Results;

/// <summary>LINQ query-syntax support over <see cref="Task{TResult}"/> of <see cref="Result{T}"/>.</summary>
public static class ResultLinqAsyncExtensions
{
    /// <summary>Async LINQ <c>from … from …</c> over a <c>Task&lt;Result&lt;T&gt;&gt;</c> source with an async binder.</summary>
    public static async Task<Result<TResult>> SelectMany<T, TMid, TResult>(
        this Task<Result<T>> source,
        Func<T, Task<Result<TMid>>> bind,
        Func<T, TMid, TResult> project)
    {
        var result = await source.ConfigureAwait(false);
        if (result.IsError)
        {
            return result.ToError<TResult>();
        }

        var value = result.UnsafeValue;
        var mid = await bind(value).ConfigureAwait(false);
        return mid.Map(m => project(value, m));
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~LinqTests"`
Expected: PASS (all 5, ×2 frameworks).

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat: add LINQ query syntax (Select/SelectMany/Where) sync and async"
```

---

### Task 9: Aggregation — `Combine` and throttled `SelectAsync`

**Files:**
- Create: `src/Vostra.Results/ResultAggregate.cs`
- Test: `tests/Vostra.Results.Tests/AggregationTests.cs`

**Interfaces:**
- Consumes: `Result`, `Result<T>` (internal `UnsafeValue`, `ErrorArray`), `Error`.
- Produces (static on the non-generic `Result` via `partial`, plus an extensions class):
  - `static Result Combine(params Result[] results)`
  - `static Result<IReadOnlyList<T>> Combine<T>(params Result<T>[] results)`
  - `static Task<Result<IReadOnlyList<TOut>>> SelectAsync<TIn,TOut>(this IEnumerable<TIn> source, Func<TIn,Task<Result<TOut>>> selector, int maxConcurrency)` on a `ResultCollectionExtensions` class.

- [ ] **Step 1: Write the failing tests `tests/Vostra.Results.Tests/AggregationTests.cs`**

```csharp
namespace Vostra.Results.Tests;

public class AggregationTests
{
    [Fact]
    public void Combine_nongeneric_is_success_when_all_succeed()
    {
        Result.Combine(Result.Success, Result.Success).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Combine_nongeneric_accumulates_all_errors()
    {
        var combined = Result.Combine(
            Result.Failure(new ValidationError("a")),
            Result.Success,
            Result.Failure(new ValidationError("b")));

        combined.IsError.Should().BeTrue();
        combined.Errors.Should().HaveCount(2);
    }

    [Fact]
    public void Combine_generic_collects_values_in_order()
    {
        Result<int> a = 1, b = 2, c = 3;

        var combined = Result.Combine(a, b, c);

        combined.IsSuccess.Should().BeTrue();
        combined.Match(v => v, _ => Array.Empty<int>()).Should().Equal(1, 2, 3);
    }

    [Fact]
    public void Combine_generic_accumulates_errors()
    {
        Result<int> a = 1;
        Result<int> b = new NotFoundError("b");
        Result<int> c = new ConflictError("c");

        var combined = Result.Combine(a, b, c);

        combined.IsError.Should().BeTrue();
        combined.Errors.Should().HaveCount(2);
    }

    [Fact]
    public async Task SelectAsync_projects_all_items_and_collects_results()
    {
        var items = new[] { 1, 2, 3 };

        Result<IReadOnlyList<int>> result =
            await items.SelectAsync(i => Task.FromResult<Result<int>>(i * 2), maxConcurrency: 2);

        result.IsSuccess.Should().BeTrue();
        result.Match(v => v, _ => Array.Empty<int>()).Should().BeEquivalentTo(new[] { 2, 4, 6 });
    }

    [Fact]
    public async Task SelectAsync_respects_the_concurrency_cap()
    {
        var current = 0;
        var peak = 0;
        var gate = new object();

        async Task<Result<int>> Track(int i)
        {
            lock (gate) { current++; peak = Math.Max(peak, current); }
            await Task.Delay(20);
            lock (gate) { current--; }
            return i;
        }

        await Enumerable.Range(0, 8).SelectAsync(Track, maxConcurrency: 3);

        peak.Should().BeLessThanOrEqualTo(3);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~AggregationTests"`
Expected: FAIL — `Combine` / `SelectAsync` do not exist.

- [ ] **Step 3: Make `Result` partial**

In `src/Vostra.Results/Result.cs`, change:
```csharp
public readonly struct Result : IEquatable<Result>
```
to:
```csharp
public readonly partial struct Result : IEquatable<Result>
```

- [ ] **Step 4: Create `src/Vostra.Results/ResultAggregate.cs`**

```csharp
namespace Vostra.Results;

public readonly partial struct Result
{
    /// <summary>Combines results, accumulating every error; success only if all succeed.</summary>
    public static Result Combine(params Result[] results)
    {
        var errors = results.Where(r => r.IsError).SelectMany(r => r.Errors).ToArray();
        return errors.Length == 0 ? Success : FromErrors(errors);
    }

    /// <summary>Combines value-results into a single result carrying all values, or all errors.</summary>
    public static Result<IReadOnlyList<T>> Combine<T>(params Result<T>[] results)
    {
        var errors = results.Where(r => r.IsError).SelectMany(r => r.ErrorArray).ToArray();
        if (errors.Length > 0)
        {
            return Result<IReadOnlyList<T>>.Err(errors);
        }

        IReadOnlyList<T> values = Array.ConvertAll(results, r => r.UnsafeValue);
        return Result<IReadOnlyList<T>>.Ok(values);
    }
}

/// <summary>Throttled async projection that collects results, porting the reference repo's SelectAsync.</summary>
public static class ResultCollectionExtensions
{
    /// <summary>
    /// Projects each item through <paramref name="selector"/> with at most
    /// <paramref name="maxConcurrency"/> concurrent invocations, then combines the results.
    /// </summary>
    public static async Task<Result<IReadOnlyList<TOut>>> SelectAsync<TIn, TOut>(
        this IEnumerable<TIn> source,
        Func<TIn, Task<Result<TOut>>> selector,
        int maxConcurrency)
    {
        if (maxConcurrency < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxConcurrency), "Must be at least 1.");
        }

        using var gate = new SemaphoreSlim(maxConcurrency);

        var tasks = source.Select(async item =>
        {
            await gate.WaitAsync().ConfigureAwait(false);
            try
            {
                return await selector(item).ConfigureAwait(false);
            }
            finally
            {
                gate.Release();
            }
        }).ToArray();

        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        return Result.Combine(results);
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~AggregationTests"`
Expected: PASS (all 6, ×2 frameworks).

- [ ] **Step 6: Run the full suite and commit**

Run: `dotnet test`
Expected: all tests pass on both `net8.0` and `net9.0`.

```bash
git add -A
git commit -m "feat: add Combine aggregation and throttled SelectAsync"
```

---

## Self-Review

**1. Spec coverage** (against `2026-06-19-vostra-result-core-design.md`):

| Spec section | Task |
|---|---|
| §4 `Result<T>` struct, no public Value, default-faulted, equality, ToString | Task 3 |
| §4 non-generic `Result`, factories, construction | Task 4 |
| §5 `Error` hierarchy + `ErrorType` taxonomy | Task 2 |
| §6 Inspect (Match/Switch/MatchFirst/SwitchFirst/TryGet/GetValueOr) | Task 5 |
| §6 Sync combinators (Map/Then/Tap/TapError/Ensure→Validation/MapError) | Task 6 |
| §6/§7 Async matrix (Task receiver + Task projections, mixed chain) | Task 7 |
| §6 LINQ (Select/SelectMany/Where, sync + async) | Task 8 |
| §6 Aggregation (Combine, collector, throttled SelectAsync) | Task 9 |
| §8 project layout, net8.0;net9.0, BCL-only, xUnit + FluentAssertions, LICENSE, notices | Task 1 |
| §9 usage-style tests at each step | every task's tests |
| §10.1 `return value` / `return new NotFoundError(...)` | Task 3 tests |
| §10.2 no invalid `.Value` (Match/TryGet only) | Task 5 (no public Value exists) |
| §10.3 Ensure/Where → Validation | Tasks 6 & 8 |
| §10.4 mixed sync/async chain, one await | Task 7 |
| §10.5 Core references only BCL | Task 1 csproj |

No gaps. (Benchmark for §10.6 zero-alloc is explicitly deferred by the spec to a later NFR task — not in this plan's scope.)

**2. Placeholder scan:** No "TBD"/"TODO"/"implement later"/"add error handling"-style steps. Every code step contains complete code. The one prose note in Task 7 Step 1 explicitly instructs how to keep the test green and is not a placeholder.

**3. Type consistency:** `ErrorType` enum values, `Error` ctor signature `(message, code, causedBy, metadata)`, the internal `Ok`/`Err`/`ToError`/`UnsafeValue`/`ErrorArray` members, and `Result.FromErrors`/`Combine` signatures are used identically across Tasks 2–9. `Result<T>` and `Result` are introduced as `partial` (Tasks 5 & 9 add the `partial` keyword before extending) so combinator/LINQ/aggregate files compile against the same type.
