namespace Vostra.Results.Tests;

public class ResultCombinatorTests
{
    private static Result Ok() => Result.Success();
    private static Result Err() => Result.Failure(new ConflictError("dup"));

    [Fact]
    public void Match_runs_success_branch()
    {
        Ok().Match(() => "ok", e => e[0].Message).Should().Be("ok");
    }

    [Fact]
    public void Match_runs_error_branch()
    {
        Err().Match(() => "ok", e => e[0].Message).Should().Be("dup");
    }

    [Fact]
    public void Switch_invokes_matching_branch()
    {
        var seen = "";
        Ok().Switch(() => seen = "ok", _ => seen = "err");
        seen.Should().Be("ok");
        Err().Switch(() => seen = "ok", _ => seen = "err");
        seen.Should().Be("err");
    }

    [Fact]
    public void Then_chains_into_a_value_result()
    {
        Result<int> r = Ok().Then(() => Result.Ok(5));
        r.Should().Be(Result.Ok(5));
        Err().Then(() => Result.Ok(5)).IsError.Should().BeTrue();
    }

    [Fact]
    public void Tap_and_TapError_fire_on_the_right_branch()
    {
        var count = 0;
        Ok().Tap(() => count++);
        Err().Tap(() => count++);
        count.Should().Be(1);

        count = 0;
        Err().TapError(_ => count++);
        Ok().TapError(_ => count++);
        count.Should().Be(1);
    }

    [Fact]
    public void Ensure_with_string_yields_validation_error()
    {
        var r = Ok().Ensure(() => false, "nope");
        r.IsError.Should().BeTrue();
        r.FirstError.Should().BeOfType<ValidationError>();
    }

    [Fact]
    public void MapError_transforms_errors()
    {
        var r = Err().MapError(e => new ValidationError(e.Message, code: "remapped"));
        r.FirstError.Code.Should().Be("remapped");
    }

    [Fact]
    public void MatchFirst_runs_success_and_first_error_branches()
    {
        Ok().MatchFirst(() => "ok", e => e.Message).Should().Be("ok");
        Err().MatchFirst(() => "ok", e => e.Message).Should().Be("dup");
    }

    [Fact]
    public void SwitchFirst_invokes_matching_branch()
    {
        var seen = "";
        Ok().SwitchFirst(() => seen = "ok", _ => seen = "err");
        seen.Should().Be("ok");
        Err().SwitchFirst(() => seen = "ok", e => seen = e.Message);
        seen.Should().Be("dup");
    }

    [Fact]
    public void Then_valueless_chains_on_success_and_propagates_on_error()
    {
        var ran = false;
        Ok().Then(() => { ran = true; return Result.Success(); }).IsSuccess.Should().BeTrue();
        ran.Should().BeTrue();

        ran = false;
        var propagated = Err().Then(() => { ran = true; return Result.Success(); });
        ran.Should().BeFalse();
        propagated.IsError.Should().BeTrue();
        propagated.FirstError.Should().BeOfType<ConflictError>();
    }

    [Fact]
    public void Ensure_with_error_already_set_skips_predicate()
    {
        var r = Err().Ensure(() => throw new InvalidOperationException("should not run"), new ValidationError("x"));
        r.IsError.Should().BeTrue();
        r.FirstError.Should().BeOfType<ConflictError>();
    }
}
