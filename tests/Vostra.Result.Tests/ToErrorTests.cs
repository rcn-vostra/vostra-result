namespace Vostra.Result.Tests;

public class ToErrorTests
{
    [Fact]
    public void ToError_propagates_a_valueless_failure_as_a_typed_result()
    {
        Result failure = Result.Failure(new NotFoundError("gone", "X.NotFound"));

        Result<int> typed = failure.ToError<int>();

        typed.IsError.Should().BeTrue();
        typed.FirstError.Code.Should().Be("X.NotFound");
    }

    [Fact]
    public void ToError_retypes_a_failed_result_of_T_preserving_errors()
    {
        Result<string> failure = new NotFoundError("gone", "X.NotFound");

        Result<int> typed = failure.ToError<int>();

        typed.IsError.Should().BeTrue();
        typed.FirstError.Code.Should().Be("X.NotFound");
    }

    [Fact]
    public void ToError_on_a_success_throws()
    {
        var fromValueless = () => Result.Ok().ToError<int>();
        var fromValued = () => Result.Ok(5).ToError<int>();

        fromValueless.Should().Throw<InvalidOperationException>();
        fromValued.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ToResult_discards_the_value_and_keeps_the_success_kind()
    {
        Result.Ok(42).ToResult().IsSuccess.Should().BeTrue();
        Result.Ok(42).ToResult().SuccessKind.Should().Be(SuccessKind.Ok);
        Result.Created(42).ToResult().SuccessKind.Should().Be(SuccessKind.Created);
    }

    [Fact]
    public void ToResult_carries_errors_on_failure()
    {
        Result<int> failure = new NotFoundError("gone", "X.NotFound");

        Result collapsed = failure.ToResult();

        collapsed.IsError.Should().BeTrue();
        collapsed.FirstError.Code.Should().Be("X.NotFound");
    }
}
