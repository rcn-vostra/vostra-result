namespace Vostra.Results.Tests;

public class ResultTests
{
    [Fact]
    public void Success_is_a_success()
    {
        Result.Ok().IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Failure_carries_the_error()
    {
        Result result = Result.Failure(new ConflictError("dup"));

        result.IsError.Should().BeTrue();
        result.FirstError.Should().BeOfType<ConflictError>();
    }

    [Fact]
    public void Default_nongeneric_result_is_faulted()
    {
        Result result = default;

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Result.Uninitialized");
    }

    [Fact]
    public void Ok_factory_builds_a_generic_success()
    {
        Result<string> result = Result.Ok("hi");

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Fail_factory_builds_a_generic_failure()
    {
        Result<string> result = Result.Fail<string>(new NotFoundError("nope"));

        result.IsError.Should().BeTrue();
        result.FirstError.Should().BeOfType<NotFoundError>();
    }
}
