namespace Vostra.Results.Testing.Tests;

public class ResultAssertionsTests
{
    [Fact]
    public void ShouldBeSuccess_returns_value_on_success()
    {
        Result<int> result = 42;

        var value = result.ShouldBeSuccess();

        value.Should().Be(42);
    }

    [Fact]
    public void ShouldBeSuccess_throws_on_failure()
    {
        Result<int> result = new NotFoundError("nope", "X.NotFound");

        var act = () => result.ShouldBeSuccess();

        act.Should().Throw<VostraAssertionException>().WithMessage("*X.NotFound*");
    }

    [Fact]
    public void ShouldHaveError_by_code_passes_when_any_error_matches()
    {
        Result<int> result = new ErrorBase[] { new ValidationError("a", "F.A"), new ValidationError("b", "F.B") };

        var chained = result.ShouldHaveError("F.B");

        chained.Should().Be(result);
    }

    [Fact]
    public void ShouldHaveError_by_code_throws_when_no_error_matches()
    {
        Result<int> result = new NotFoundError("nope", "X.NotFound");

        var act = () => result.ShouldHaveError("Other.Code");

        act.Should().Throw<VostraAssertionException>();
    }

    [Fact]
    public void ShouldHaveError_by_code_throws_on_success_without_no_errors_suffix()
    {
        Result<int> ok = 1;

        var act = () => ok.ShouldHaveError("Some.Code");

        act.Should().Throw<VostraAssertionException>()
            .WithMessage("*successful*")
            .And.Message.Should().NotContain("(no errors)");
    }

    [Fact]
    public void ShouldHaveError_by_type_throws_on_success()
    {
        Result<int> ok = 1;

        var act = () => ok.ShouldHaveError(ErrorType.NotFound);

        act.Should().Throw<VostraAssertionException>();
    }

    [Fact]
    public void ShouldHaveError_by_type_passes()
    {
        Result<int> result = new NotFoundError("nope", "X.NotFound");

        result.ShouldHaveError(ErrorType.NotFound);
    }

    [Fact]
    public void ShouldBeNotFound_sugar_passes()
    {
        Result<int> result = new NotFoundError("nope", "X.NotFound");

        result.ShouldBeNotFound();
    }

    [Fact]
    public void Assert_runs_action_on_success_and_returns_result()
    {
        Result<int> result = 10;
        var seen = 0;

        var chained = result.Assert(v => seen = v);

        seen.Should().Be(10);
        chained.Should().Be(result);
    }

    [Fact]
    public void Assert_throws_when_result_is_failure()
    {
        Result<int> result = new NotFoundError("nope", "X.NotFound");

        var act = () => result.Assert(_ => { });

        act.Should().Throw<VostraAssertionException>();
    }

    [Fact]
    public void Assert_noarg_returns_result_on_success()
    {
        Result<int> result = 5;

        var chained = result.Assert();

        chained.Should().Be(result);
    }

    [Fact]
    public void Assert_noarg_throws_on_failure()
    {
        Result<int> result = new NotFoundError("nope", "X.NotFound");

        var act = () => result.Assert();

        act.Should().Throw<VostraAssertionException>();
    }

    [Fact]
    public void Assert_noarg_nongeneric_returns_result_on_success()
    {
        var result = Result.Success;

        var chained = result.Assert();

        chained.Should().Be(result);
    }

    [Fact]
    public void Assert_noarg_nongeneric_throws_on_failure()
    {
        Result result = new NotFoundError("nope", "X.NotFound");

        var act = () => result.Assert();

        act.Should().Throw<VostraAssertionException>();
    }

    [Fact]
    public void Failure_message_includes_request_context_when_present()
    {
        var error = new NotFoundError("gone", "X.NotFound")
            .WithMetadata(new Dictionary<string, object?> { ["request"] = new RequestContext("GET", "products/9", null) });
        Result<int> result = (ErrorBase)error;

        var act = () => result.ShouldBeSuccess();

        act.Should().Throw<VostraAssertionException>()
            .WithMessage("*GET*products/9*");
    }

    [Fact]
    public void WithRequestContext_attaches_and_Describe_renders_neutral_fields()
    {
        ErrorBase error = new NotFoundError("missing", "Thing.NotFound")
            .WithRequestContext(new RequestContext("SEND", "wo-inbound", "payload"));

        Result<int> result = error;

        var ex = Assert.Throws<VostraAssertionException>(() => result.ShouldBeSuccess());
        ex.Message.Should().Contain("SEND wo-inbound");
        ex.Message.Should().Contain("body: payload");
    }
}
