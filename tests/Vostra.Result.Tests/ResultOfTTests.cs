namespace Vostra.Result.Tests;

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
        result.FirstError.Should().BeOfType<Error>();
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

    [Fact]
    public void Error_array_converts_implicitly_to_failure()
    {
        Result<int> result = new ErrorBase[] { new NotFoundError("a"), new ConflictError("b") };

        result.IsError.Should().BeTrue();
        result.Errors.Should().HaveCount(2);
    }

    [Fact]
    public void Error_list_converts_implicitly_to_failure()
    {
        Result<int> result = new List<ErrorBase> { new ValidationError("a") };

        result.IsError.Should().BeTrue();
        result.FirstError.Should().BeOfType<ValidationError>();
    }

    [Fact]
    public void Null_reference_success_does_not_throw_on_hashcode()
    {
        Result<string?> result = (string?)null;

        result.IsSuccess.Should().BeTrue();
        var act = () => result.GetHashCode();
        act.Should().NotThrow();
    }
}
