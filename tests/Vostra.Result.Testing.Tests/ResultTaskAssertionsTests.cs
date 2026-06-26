namespace Vostra.Result.Testing.Tests;

public class ResultTaskAssertionsTests
{
    private static Task<Result<int>> OkAsync(int v) => Task.FromResult<Result<int>>(v);
    private static Task<Result<int>> FailAsync() => Task.FromResult<Result<int>>(new NotFoundError("nope", "X.NotFound"));

    [Fact]
    public async Task ShouldBeSuccess_awaits_and_returns_value()
    {
        var value = await OkAsync(7).ShouldBeSuccess();

        value.Should().Be(7);
    }

    [Fact]
    public async Task ShouldHaveError_awaits_and_chains()
    {
        var chained = await FailAsync().ShouldHaveError("X.NotFound");

        chained.IsError.Should().BeTrue();
    }

    [Fact]
    public async Task ShouldBeNotFound_async_sugar_passes()
    {
        await FailAsync().ShouldBeNotFound();
    }

    [Fact]
    public async Task Assert_async_runs_and_chains()
    {
        var seen = 0;

        await OkAsync(9).Assert(v => seen = v);

        seen.Should().Be(9);
    }

    [Fact]
    public async Task ShouldBeSuccess_async_throws_on_failure()
    {
        var act = async () => await FailAsync().ShouldBeSuccess();

        await act.Should().ThrowAsync<VostraAssertionException>();
    }

    [Fact]
    public async Task Assert_noarg_async_chains_on_success()
    {
        var chained = await OkAsync(3).Assert();

        chained.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Assert_noarg_async_throws_on_failure()
    {
        var act = async () => await FailAsync().Assert();

        await act.Should().ThrowAsync<VostraAssertionException>();
    }
}
