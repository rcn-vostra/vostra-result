namespace Vostra.Results.Tests;

public class SyncCombinatorTests
{
    private static Result<int> Ok(int v) => v;
    private static Result<int> Err() => new NotFoundError("missing");

    [Fact]
    public void Map_transforms_success()
    {
        Ok(2).Map(v => v * 10).Should().Be(Result.Ok(20));
    }

    [Fact]
    public void Map_propagates_error()
    {
        Err().Map(v => v * 10).IsError.Should().BeTrue();
    }

    [Fact]
    public void Then_chains_success()
    {
        Result<int> Half(int v) => v % 2 == 0 ? v / 2 : new ValidationError("odd");

        Ok(8).Then(Half).Should().Be(Result.Ok(4));
        Ok(7).Then(Half).FirstError.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public void Tap_runs_only_on_success_and_passes_through()
    {
        var seen = 0;
        var result = Ok(5).Tap(v => seen = v);

        seen.Should().Be(5);
        result.Should().Be(Result.Ok(5));

        seen = 0;
        Err().Tap(v => seen = v);
        seen.Should().Be(0);
    }

    [Fact]
    public void TapError_runs_only_on_error()
    {
        var count = 0;
        Err().TapError(_ => count++);
        Ok(1).TapError(_ => count++);
        count.Should().Be(1);
    }

    [Fact]
    public void Ensure_with_string_defaults_to_validation_error()
    {
        var result = Ok(3).Ensure(v => v > 10, "too small");

        result.IsError.Should().BeTrue();
        result.FirstError.Should().BeOfType<ValidationError>();
        result.FirstError.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public void Ensure_passes_through_when_predicate_holds()
    {
        Ok(20).Ensure(v => v > 10, "too small").Should().Be(Result.Ok(20));
    }

    [Fact]
    public void MapError_transforms_each_error()
    {
        var result = Err().MapError(e => new ValidationError(e.Message, code: "remapped"));

        result.FirstError.Should().BeOfType<ValidationError>();
        result.FirstError.Code.Should().Be("remapped");
    }
}
