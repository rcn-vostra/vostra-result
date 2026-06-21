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
