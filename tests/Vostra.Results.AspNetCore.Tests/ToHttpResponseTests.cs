using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Vostra.Results.AspNetCore.Tests;

public class ToHttpResponseTests
{
    private static DefaultHttpContext Context(VostraResultsOptions? options = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
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

    // ---- success paths ----

    [Fact]
    public async Task Ok_value_returns_200_success_envelope()
    {
        Result<int> result = 42;
        var ctx = Context();
        var res = await HttpResultHarness.Execute(result.ToHttpResponse(ctx), ctx.RequestServices);

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
    public async Task OperationId_prefers_current_activity()
    {
        using var activity = new Activity("test").Start();
        Result<int> result = 1;
        var ctx = Context();
        var res = await HttpResultHarness.Execute(result.ToHttpResponse(ctx), ctx.RequestServices);

        res.Json.GetProperty("operationId").GetString().Should().Be(activity.Id);
    }

    // ---- error paths (exercise ProblemResultBuilder via the public API) ----

    [Fact]
    public async Task Error_returns_problem_details_with_code_and_errorType()
    {
        Result<int> result = new NotFoundError("Order 42 not found", code: "Order.NotFound");
        var ctx = Context(); // no AddVostraResults -> default fallback
        var res = await HttpResultHarness.Execute(result.ToHttpResponse(ctx), ctx.RequestServices);

        res.Status.Should().Be(404);
        res.ContentType.Should().StartWith("application/problem+json");
        res.Json.GetProperty("status").GetInt32().Should().Be(404);
        res.Json.GetProperty("type").GetString().Should().Be("about:blank");
        res.Json.GetProperty("title").GetString().Should().Be("Not Found");
        res.Json.GetProperty("detail").GetString().Should().Be("Order 42 not found");
        res.Json.GetProperty("code").GetString().Should().Be("Order.NotFound");
        res.Json.GetProperty("errorType").GetString().Should().Be("NotFound");
        res.Json.GetProperty("operationId").GetString().Should().Be("trace-xyz");
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
    public async Task Multiple_non_validation_errors_use_first_for_status_and_list_all()
    {
        Result<int> result = new ErrorBase[]
        {
            new ConflictError("Order is locked", code: "Order.Locked"),
            new Error("Version mismatch", code: "Order.Stale"),
        };
        var ctx = Context();
        var res = await HttpResultHarness.Execute(result.ToHttpResponse(ctx), ctx.RequestServices);

        res.Status.Should().Be(409);
        res.Json.GetProperty("code").GetString().Should().Be("Order.Locked");
        var errors = res.Json.GetProperty("errors");
        errors.ValueKind.Should().Be(JsonValueKind.Array);
        errors.GetArrayLength().Should().Be(2);
        errors[0].GetProperty("code").GetString().Should().Be("Order.Locked");
        errors[1].GetProperty("code").GetString().Should().Be("Order.Stale");
    }

    [Fact]
    public async Task All_validation_errors_render_field_map_keyed_by_metadata_field()
    {
        var skuField = new Dictionary<string, object?> { ["field"] = "Sku" };
        var priceField = new Dictionary<string, object?> { ["field"] = "Price" };
        Result<int> result = new ErrorBase[]
        {
            new ValidationError("required", metadata: skuField),
            new ValidationError("must be > 0", metadata: priceField),
        };
        var ctx = Context();
        var res = await HttpResultHarness.Execute(result.ToHttpResponse(ctx), ctx.RequestServices);

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
        Result<int> result = new ValidationError("required", code: "Sku.Required");
        var ctx = Context();
        var res = await HttpResultHarness.Execute(result.ToHttpResponse(ctx), ctx.RequestServices);

        res.Status.Should().Be(400);
        res.Json.GetProperty("errors").GetProperty("Sku.Required")[0].GetString().Should().Be("required");
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
