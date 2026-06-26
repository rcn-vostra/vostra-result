using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Vostra.Result.AspNetCore.Tests;

public class WireContractTests
{
    private static DefaultHttpContext Context()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        return new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(),
            TraceIdentifier = "t-1",
        };
    }

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
