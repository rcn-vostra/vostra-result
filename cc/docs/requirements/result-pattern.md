# The `Result` Pattern & `AM.Extensions` (reference: existing implementation)

> **Reference repository (source of truth for "today"):**
> `C:\Users\Robert\source\repos\_ARCHIVE\VCC\popcat-assortment-admin-api`
> All `file:line` links below point into that repo. This document describes the **existing**
> FluentResults-based implementation that the `vostra-result` library is intended to replace —
> read it to understand what to preserve and what to fix (see `result-type-requirements.md`).

This document describes how that codebase represents success/failure across layers using
[FluentResults](https://github.com/altmann/FluentResults) `Result` / `Result<T>`, the helper
extensions in **`AM.Extensions`**, how those Results are turned into HTTP responses by
**`AM.Extensions.AspNetCore`**, and how the integration tests consume the same `Result<T>` type
through `IntegrationTestHelper`.

> TL;DR — *Errors are values, not exceptions.* A service returns `Result<T>`. The "kind" of
> failure (validation / not-found / conflict) is carried as a **typed `IError`** subclass; the
> "kind" of success (200 vs 201) is carried as a **typed `ISuccess`** subclass. The controller base
> class maps those types to HTTP status codes. The integration tests deserialize the HTTP envelope
> back into the *same* `Result<T>` so they can assert with the same vocabulary.

---

## 1. Why FluentResults

The business/service layer never throws for expected, domain-level failures (a missing entity, a
duplicate, a validation breach). Instead it returns a `Result` (no payload) or `Result<T>` (with a
payload). This:

- keeps control flow explicit and composable (`Bind`, `Map`, `SelectMany`, …),
- lets a single place (`ApiControllerBase`) translate a failure into the correct HTTP status,
- and lets tests treat an HTTP failure exactly like a service-layer failure.

`AM.Extensions` only references the `FluentResults` NuGet package — nothing else. It is the lowest
common building block in the solution.

---

## 2. Typed statuses & errors

FluentResults lets you attach arbitrary `ISuccess` / `IError` reasons to a result. The codebase
defines a small, fixed vocabulary of these so that the *meaning* of a result is type-checked rather
than string-matched.

Location: [`Modules/AM.Extensions/AM.Extensions/ResultExtensions/`](C:/Users/Robert/source/repos/_ARCHIVE/VCC/popcat-assortment-admin-api/Modules/AM.Extensions/AM.Extensions/ResultExtensions/)

### Success reasons (`ISuccess`)

| Type | File | Maps to HTTP |
|------|------|--------------|
| `OkResult` | [OkResult.cs](C:/Users/Robert/source/repos/_ARCHIVE/VCC/popcat-assortment-admin-api/Modules/AM.Extensions/AM.Extensions/ResultExtensions/OkResult.cs) | `200 OK` |
| `CreatedResult` (`: OkResult`) | [CreatedResult.cs](C:/Users/Robert/source/repos/_ARCHIVE/VCC/popcat-assortment-admin-api/Modules/AM.Extensions/AM.Extensions/ResultExtensions/CreatedResult.cs) | `201 Created` |

`CreatedResult` inherits from `OkResult`, so anything that is "created" is also "ok".

### Error reasons (`IError`, all extend FluentResults' `Error`)

| Type | File | Maps to HTTP |
|------|------|--------------|
| `ValidationError` | [ValidationError.cs](C:/Users/Robert/source/repos/_ARCHIVE/VCC/popcat-assortment-admin-api/Modules/AM.Extensions/AM.Extensions/ResultExtensions/ValidationError.cs) | `400 Bad Request` |
| `NotFoundError` | [NotFoundError.cs](C:/Users/Robert/source/repos/_ARCHIVE/VCC/popcat-assortment-admin-api/Modules/AM.Extensions/AM.Extensions/ResultExtensions/NotFoundError.cs) | `404 Not Found` |
| `AlreadyExistError` | [AlreadyExistError.cs](C:/Users/Robert/source/repos/_ARCHIVE/VCC/popcat-assortment-admin-api/Modules/AM.Extensions/AM.Extensions/ResultExtensions/AlreadyExistError.cs) | `409 Conflict` |
| *(plain `Error`)* | — | `500 Internal Server Error` (the fallback) |

---

## 3. `ResultStatus` — the factory

[`ResultStatus.cs`](C:/Users/Robert/source/repos/_ARCHIVE/VCC/popcat-assortment-admin-api/Modules/AM.Extensions/AM.Extensions/ResultExtensions/ResultStatus.cs) is the
**preferred way to construct results** in the service layer. It wraps `Result.Ok` / `Result.Fail`
and attaches the right typed reason from §2. Prefer these over calling `Result.Ok()` /
`Result.Fail()` directly, so the HTTP-status mapping in §6 works.

```csharp
ResultStatus.Ok();                  // Result            -> 200, tagged OkResult
ResultStatus.Ok(value);             // Result<T>         -> 200, tagged OkResult
ResultStatus.Created(value);        // Result<T>         -> 201, tagged CreatedResult

ResultStatus.Error("boom");         // Result / Result<T> -> 500 (plain Error)
ResultStatus.Error("boom", ex);     // attaches the exception via .CausedBy(ex)

ResultStatus.NotFound("no such id");        // -> 404
ResultStatus.AlreadyExist("dupe");          // -> 409
ResultStatus.ValidationFailed("bad input"); // -> 400
ResultStatus.ValidationFailed(errors);      // re-wraps a set of IError as ValidationErrors
```

Both non-generic (`Result`) and generic (`Result<T>`) overloads exist for each.

---

## 4. `ResultExtensions` — composition & inspection

[`ResultExtensions.cs`](C:/Users/Robert/source/repos/_ARCHIVE/VCC/popcat-assortment-admin-api/Modules/AM.Extensions/AM.Extensions/ResultExtensions/ResultExtensions.cs)

### Async binding

FluentResults ships `Bind` for synchronous chaining; this file adds the **async** variants and a few
ergonomics helpers.

```csharp
// Continue only on success; short-circuits errors (forwards them to the new type).
Task<Result<TNew>> BindAsync<T,TNew>(this Task<Result<T>> task, Func<T, Task<Result<TNew>>> f)
Task<Result<TNew>> BindAsync<T,TNew>(this Result<T> result,   Func<T, Task<Result<TNew>>> f)
```

`BindAsync` is the backbone of the controller/test flow: it lets you chain an HTTP call (or a repo
call) onto a previous success while automatically propagating any earlier failure.

### Validation in a chain

```csharp
Result<T>        ValidateThat<T>(this Result<T> result, Func<T,bool> predicate, string errorMessage)
Task<Result<T>>  ValidateThat<T>(this Task<Result<T>> task, Func<T,bool> predicate, string errorMessage)
```

If `predicate(value)` is false the result becomes a plain `Error` (`ResultStatus.Error`), otherwise
the value passes through. **Note:** this maps to 500, not 400 — a bug the new library fixes (P8).

### Side effects & conversion

```csharp
Result<T> Tap<T>(this Result<T> result, Action<T> action)   // run an action on success, pass result through
Result    ToResult<T>(this Result<T> result, string contextMessage) // drop the value, prefix a context error
```

### Deconstruction

```csharp
var (value, result) = someResult.Deconstruct();          // sync
var (value, result) = await someTaskResult.Deconstruct(); // async
// NOTE: 'value' is default! when the result failed — always check result.IsSuccess first.
```

### Status / error inspection

```csharp
result.IsOk()               // has an OkResult success reason
result.IsCreated()          // has a CreatedResult success reason
result.IsNotFound()         // has a NotFoundError
result.IsAlreadyExist()     // has an AlreadyExistError
result.IsValidationFailed() // has a ValidationError

result.HasStatus<T,TStatus>() // generic: any success reason of type TStatus
result.HasError<T,TError>()   // generic: any error reason of type TError
```

### Error formatting

```csharp
result.ErrorsStr();            // join all error messages with ", "
result.ErrorsStr(delimiter);   // custom delimiter; works on Result and Result<T>
```

`ErrorsStr()` is used everywhere — in the controller (to build the HTTP error description) and in
tests (to print why an assertion failed).

---

## 5. `ResultSelectExtensions` — LINQ query syntax

[`ResultSelectExtensions.cs`](C:/Users/Robert/source/repos/_ARCHIVE/VCC/popcat-assortment-admin-api/Modules/AM.Extensions/AM.Extensions/ResultExtensions/ResultSelectExtensions.cs)
provides `SelectMany` overloads so you can compose `Task<Result<T>>` with C# **query syntax**. This
is used in the service layer to read like a sequence of dependent steps where any failure
short-circuits the whole expression:

```csharp
var builtProductResult = await (
    from product  in BuildProduct(request)
    from variants in BuildProductVariants(request, product.Id, product.ProductGroupId!, codes)
    select WithVariants(product, variants)
);
```

(See [`ProductDetailsService.Create`](C:/Users/Robert/source/repos/_ARCHIVE/VCC/popcat-assortment-admin-api/Modules/AM.ProductDetails/AM.ProductDetails.Core/Services/ProductDetailsService.cs).)

There are two overloads — one where the bound step is async (`Func<T1, Task<Result<T2>>>`) and one
where it is sync (`Func<T1, Result<T2>>`). Both require `T1`/`T2` to be `notnull`.

---

## 6. `CollectionsExtensions`

[`CollectionsExtensions.cs`](C:/Users/Robert/source/repos/_ARCHIVE/VCC/popcat-assortment-admin-api/Modules/AM.Extensions/AM.Extensions/CollectionsExtensions.cs) — not
Result-specific, but lives in the same package.

```csharp
// Throttled async Select. Runs at most maxTasksAtATime tasks concurrently.
// Used to avoid "A second operation was started on this context..." with EF Core DbContext.
Task<IEnumerable<TResult>> SelectAsync<TSource,TResult>(
    this IEnumerable<TSource> source, Func<TSource,Task<TResult>> method, int maxTasksAtATime = int.MaxValue)

item.IsIn(collection);     // collection.Contains(item)
item.IsNotIn(collection);  // !collection.Contains(item)
```

`SelectAsync(..., maxTasksAtATime: 1)` is used to serialize per-item DbContext work
(see `BuildProductVariants`).

---

## 7. `AM.Extensions.AspNetCore` — Result → HTTP

[`ApiControllerBase.cs`](C:/Users/Robert/source/repos/_ARCHIVE/VCC/popcat-assortment-admin-api/Modules/AM.Extensions/AM.Extensions.AspNetCore/Controller/ApiControllerBase.cs)
is the abstract base every API controller derives from. It owns the **single** place where a
`Result<T>` becomes an HTTP response, and where the `OperationId` (correlation id) is stamped onto the
envelope.

```csharp
protected ObjectResult ToHttpResponse<T>(Result<T> result) where T : class;
protected ObjectResult ToHttpResponse<T,P>(Result<IEnumerable<T>> result, P pagination) where P : Pagination;
protected ObjectResult ToHttpResponse<T,P>(Result<ICollection<T>> result, P pagination) where P : Pagination;
```

- **Success** → wraps the value in `SuccessResponse<T>` (or `ListResponse<T,P>` for collections) with
  `Status` = `201` when `result.IsCreated()`, otherwise `200`.
- **Failure** → `ErrorResponse` whose status comes from the typed error:

  ```csharp
  result switch {
      _ when r.IsValidationFailed() => 400, "Validation(s) failed: ..."
      _ when r.IsNotFound()         => 404, "Not found: ..."
      _ when r.IsAlreadyExist()     => 409, "Already exists: ..."
      _                             => 500, "Internal error: ..."   // any other error
  }
  ```

Typical controller usage (note `result.Map(...)` to project domain → response DTO before handing to
`ToHttpResponse`):

```csharp
var result = await _productService.GetById((ProductDetailId)id);
return ToHttpResponse(result.Map(_mapper.Map<ProductDetailedResponse>));
```

The HTTP envelopes (`SuccessResponse<T>`, `ListSuccessResponse<T>`, `ErrorResponse`, `Pagination`)
come from the shared `VolvoCars.API.Common.Models` package and the API project's `Entities` folder
(e.g. [`SuccessResponse.cs`](C:/Users/Robert/source/repos/_ARCHIVE/VCC/popcat-assortment-admin-api/Modules/AM.ProductDetails/AM.ProductDetails.Api/Entities/SuccessResponse.cs)).
A success body looks like `{ "status": 200, "operationId": "...", "data": { ... } }`.

---

## 8. `IntegrationTestHelper` — closing the loop

[`IntegrationTestHelper.cs`](C:/Users/Robert/source/repos/_ARCHIVE/VCC/popcat-assortment-admin-api/Modules/AM.ProductDetails/test/AM.ProductDetails.IntegrationTests/Helpers/IntegrationTestHelper.cs)

This is the key insight that ties the doc together: the integration tests do **not** assert on raw
`HttpResponseMessage`. Instead the helper translates an HTTP round-trip back into a
`Result<TResponse>`, so a test can chain and assert using the *same* `BindAsync` / `ErrorsStr` /
`Result` vocabulary used in the service layer.

### Construction

```csharp
var helper = new IntegrationTestHelper(_client); // baseUrl defaults to "api/v2/details/admin"
```

### The verbs

```csharp
Task<Result<TResponse>> Get<TResponse>(string urlPart = "")
Task<Result<TResponse>> Post<TResponse>(object content)               // empty urlPart
Task<Result<TResponse>> Post<TResponse>(string urlPart, object content)
Task<Result<TResponse>> Put<TResponse>(object content)
Task<Result<TResponse>> Put<TResponse>(string urlPart, object content)
Task<Result<TResponse>> Delete<TResponse>(string urlPart)
```

Each verb:

1. Sends the request (`content` serialized with Newtonsoft).
2. On a non-success status code → `Result.Fail(...)` with a **rich** message built by `FailMessage`:
   verb, URL, status code + reason phrase, the request body that was sent, and the **internal error
   message** dug out of the response body.
3. On success → deserializes the body as `SuccessResponse<TResponse>` and returns `.Data`
   (or `Result.Fail(...)` if data was null).

### Unwrapping the envelope (`DeserializeContent<T>`)

- Deserializes into `SuccessResponse<T>` (the §7 envelope) and returns `res?.Data`.
- Uses strict settings: `MissingMemberHandling = Error`. If the test's `TResponse` DTO doesn't match
  the JSON the controller actually returned, it throws a `JsonSerializationException` with an
  **added hint**: *"MAKE SURE YOU HAVE MAPPED TO THE CORRECT RESPONSE OBJECT…"*. This is a deliberate
  guard against silently testing against the wrong response shape.
- `StringEnumConverter` is registered, and `NullValueHandling = Ignore`.

### Surfacing the server's error (`GetInternalErrorMessage`)

When a call fails, the helper tries hard to give you a readable reason:

- Plain (non-JSON) body → returned verbatim.
- A `{ "error": { message, description } }` envelope (`ErrorResponse`) → `"{message} ({description})"`.
- Otherwise tries the ASP.NET `ValidationProblemDetails` shape (`AspNetErrorResponse`, defined inline)
  and joins its `errors` dictionary into `"key : v1, v2"` lines.

This means a failing assertion prints *why* the server rejected the request, not just `400`.

### `CollectionExtensions` (bottom of the same file)

Small string-join helpers used by `GetInternalErrorMessage`:

```csharp
coll.JoinProperty(selector, delimiter = "");  // null/empty-safe string.Join over a projection
coll.JoinStr(delimiter = ", ");               // null/empty-safe string.Join over strings
```

---

## 9. How tests actually read

The companion test-only helpers turn a `Result<T>` into FluentAssertions checks:

- [`FluentResultExtensions.cs`](C:/Users/Robert/source/repos/_ARCHIVE/VCC/popcat-assortment-admin-api/Modules/AM.ProductDetails/test/AM.ProductDetails.IntegrationTests/Helpers/FluentResultExtensions.cs)
  — `.Assert()` (sync and `Task<Result<T>>` async). It asserts `result.Should().BeSuccess(...)`
  (printing `ErrorsStr()` on failure), runs an optional `Action<T>` of inner assertions, and
  **returns the same result** so the chain continues.
- [`StringAssertionExtensions.cs`](C:/Users/Robert/source/repos/_ARCHIVE/VCC/popcat-assortment-admin-api/Modules/AM.ProductDetails/test/AM.ProductDetails.IntegrationTests/Helpers/StringAssertionExtensions.cs)
  — `BeNonNullGuidFormatted(propertyName)` for asserting a string is a parseable, non-empty `Guid`.

Putting it all together — a real test flow from
[`TestProducts.cs`](C:/Users/Robert/source/repos/_ARCHIVE/VCC/popcat-assortment-admin-api/Modules/AM.ProductDetails/test/AM.ProductDetails.IntegrationTests/TestProducts.cs):

```csharp
await helper.Post<ProductResponse>("/products/", creationRequest)
            .Assert()                                                  // POST succeeded (chain stops here if not)
            .BindAsync(p => helper.Get<ProductDetailedResponse>($"/products/{p.Id}"))  // GET the created product
            .Assert(p =>                                               // assert on the fetched payload
            {
                p.Id.Should().Be(productIdStr);
                p.Variants.Should().HaveCount(1);
                p.Id.Should().BeNonNullGuidFormatted(nameof(p.Id));
            });
```

And asserting a *failure* path uses the same `Result`:

```csharp
var result = await helper.Post<ProductResponse>("/products/", creationRequest);
result.IsSuccess.Should().BeFalse();
result.ErrorsStr().Should().Contain("Values for some specification(s) are missing: 'cable_length')");
```

`IntegrationTestHelper` is currently used by the test files under
`Modules/AM.ProductDetails/test/AM.ProductDetails.IntegrationTests/`: `TestProducts`,
`TestProductVariants`, `TestPackageDetails`, `TestPackageVariants`, `TestProductGroups`,
`TestSpecifications`, `TestClassifications`.

---

## 10. End-to-end flow at a glance

```
Service layer            ApiControllerBase            HTTP wire                IntegrationTestHelper        Test
─────────────            ─────────────────            ─────────                ─────────────────────        ────
ResultStatus.Created(x)  ToHttpResponse(result        201 + SuccessResponse    Post<T>() deserializes       .Assert()
 / NotFound(...)  ──────▶  .Map(toDto))        ──────▶ { status, operationId,   envelope back into   ──────▶ .BindAsync(...)
 / ValidationFailed(...)   maps typed reason            data }  / ErrorResponse  Result<T>                    .ErrorsStr()
                           → 200/201/400/404/409/500
```

The same `Result<T>` type and the same extension vocabulary (`BindAsync`, `IsCreated`,
`ErrorsStr`, …) are used on **both** ends of the wire — that symmetry is the whole point of this
design.
