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
