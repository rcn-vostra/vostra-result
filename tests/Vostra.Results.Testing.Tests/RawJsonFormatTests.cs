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
