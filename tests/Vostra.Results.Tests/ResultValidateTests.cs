namespace Vostra.Results.Tests;

public class ResultValidateTests
{
    [Fact]
    public void All_passing_checks_yield_success()
    {
        var result = Result.Ok()
            .Validate(true, "a")
            .Validate(true, "b");

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Accumulates_every_failed_check_in_order()
    {
        var result = Result.Ok()
            .Validate(false, "email required", field: "email")
            .Validate(true, "age fine", field: "age")
            .Validate(false, "name required", field: "name");

        result.IsError.Should().BeTrue();
        result.Errors.Should().HaveCount(2); // the passing check contributes nothing
        result.Errors.Select(e => e.Message).Should().Equal("email required", "name required");
    }

    [Fact]
    public void Builds_a_ValidationError_carrying_field_and_code()
    {
        var error = Result.Ok()
            .Validate(false, "Email is invalid", field: "email", code: "Email.Invalid")
            .FirstError;

        error.Should().BeOfType<ValidationError>();
        error.Type.Should().Be(ErrorType.Validation);
        error.Code.Should().Be("Email.Invalid");
        error.Metadata![ErrorBase.FieldMetadataKey].Should().Be("email");
    }

    [Fact]
    public void Defaults_to_the_validation_fallback_code_when_unspecified()
    {
        Result.Ok().Validate(false, "bad").FirstError.Code.Should().Be("General.Validation");
    }
}
