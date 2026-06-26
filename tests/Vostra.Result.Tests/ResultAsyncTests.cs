namespace Vostra.Result.Tests;

public class ResultAsyncTests
{
    private static Task<Result> OkAsync() => Task.FromResult(Result.Ok());
    private static Task<Result> ErrAsync() => Task.FromResult(Result.Failure(new ConflictError("dup")));

    [Fact]
    public async Task Mixed_chain_resolves_with_one_await()
    {
        Result<int> r = await OkAsync()
            .Then(() => Result.Ok())                       // sync valueless step
            .Then(() => Task.FromResult(Result.Ok()))      // async valueless step
            .Then(() => Result.Ok(7));                        // chain into a value
        r.Should().Be(Result.Ok(7));
    }

    [Fact]
    public async Task Error_short_circuits()
    {
        Result<int> r = await ErrAsync().Then(() => Result.Ok(7));
        r.IsError.Should().BeTrue();
        r.FirstError.Should().BeOfType<ConflictError>();
    }

    [Fact]
    public async Task TapError_async_runs_on_failure()
    {
        var count = 0;
        await ErrAsync().TapError(_ => { count++; return Task.CompletedTask; });
        count.Should().Be(1);
    }
}
