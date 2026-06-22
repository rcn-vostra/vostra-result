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
        ctx.Operation.Should().Be("POST");
        ctx.Target.Should().Be("products/x");
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

    [Fact]
    public async Task Empty_error_list_attaches_request_context()
    {
        var fakeFormat = new FakeRawFormat(Array.Empty<ErrorBase>());
        var http = StubHttpMessageHandler.Client(HttpStatusCode.BadRequest, "{}");
        var api = new TestHttpClient(http, "items", fakeFormat);

        var result = await api.Get<Product>("/x");

        result.IsError.Should().BeTrue();
        result.FirstError.Metadata.Should().ContainKey("request");
        var ctx = (RequestContext)result.FirstError.Metadata!["request"]!;
        ctx.Operation.Should().Be("GET");
        ctx.Target.Should().Be("items/x");
    }

    [Fact]
    public async Task Pre_existing_metadata_is_preserved_alongside_request_context()
    {
        var seed = new Error("seeded error", "Seed.Code");
        var withMeta = seed.WithMetadata(new Dictionary<string, object?> { ["seed"] = 123 });
        var fakeFormat = new FakeRawFormat(new ErrorBase[] { withMeta });
        var http = StubHttpMessageHandler.Client(HttpStatusCode.BadRequest, "{}");
        var api = new TestHttpClient(http, "", fakeFormat);

        var result = await api.Get<Product>("/y");

        result.IsError.Should().BeTrue();
        result.FirstError.Metadata.Should().ContainKey("seed");
        result.FirstError.Metadata.Should().ContainKey("request");
        result.FirstError.Metadata!["seed"].Should().Be(123);
    }

    [Fact]
    public async Task GetList_success_returns_items_and_pagination()
    {
        const string listJson = """
            {
                "operationId":"op",
                "data":[{"id":1,"name":"a"},{"id":2,"name":"b"}],
                "pagination":{"page":1,"pageSize":20,"totalCount":2,"totalPages":1}
            }
            """;
        var http = StubHttpMessageHandler.Client(HttpStatusCode.OK, listJson);
        var api = new TestHttpClient(http);

        var result = await api.GetList<Product>("");

        result.IsSuccess.Should().BeTrue();
        result.TryGetValue(out var page).Should().BeTrue();
        page!.Items.Should().HaveCount(2);
        page.Pagination.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task Success_with_null_payload_returns_error_with_request_context()
    {
        // A 2xx response whose payload deserializes to null still yields an error result —
        // and that error must carry the request context like every other failure path.
        var fakeFormat = new FakeRawFormat(Array.Empty<ErrorBase>());
        var http = StubHttpMessageHandler.Client(HttpStatusCode.OK, """{ "data": null }""");
        var api = new TestHttpClient(http, "products", fakeFormat);

        var result = await api.Get<Product>("/7");

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Http.NullResponse");
        result.FirstError.Metadata.Should().ContainKey("request");
        ((RequestContext)result.FirstError.Metadata!["request"]!).Target.Should().Be("products/7");
    }
}
