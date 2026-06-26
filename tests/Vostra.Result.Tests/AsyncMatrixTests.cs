namespace Vostra.Result.Tests;

public class AsyncMatrixTests
{
    private static Task<Result<int>> GetAsync(int v) => Task.FromResult<Result<int>>(v);
    private static Task<Result<int>> DoubleAsync(int v) => Task.FromResult<Result<int>>(v * 2);

    [Fact]
    public async Task Mixed_sync_and_async_chain_uses_one_leading_await()
    {
        Result<string> result = await GetAsync(5)            // Task<Result<int>>
            .Ensure(v => v > 0, "must be positive")           // sync guard, mid-async
            .Then(v => DoubleAsync(v))                         // async T -> Task<Result>
            .Map(v => v + 1)                                   // sync map
            .Map(v => $"={v}");                                // sync map

        result.IsSuccess.Should().BeTrue();
        result.Match(s => s, _ => "err").Should().Be("=11");
    }

    [Fact]
    public async Task Error_short_circuits_the_async_chain()
    {
        Result<int> result = await GetAsync(-1)
            .Ensure(v => v > 0, "must be positive")
            .Then(v => DoubleAsync(v));

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task Async_projection_on_sync_receiver_lifts_to_task()
    {
        Result<int> seed = 4;
        Result<int> result = await seed.Then(v => DoubleAsync(v));
        result.Should().Be(Result.Ok(8));
    }

    [Fact]
    public async Task TapError_async_runs_on_failure()
    {
        var count = 0;
        Result<int> err = new NotFoundError("x");

        await Task.FromResult(err).TapError(_ => { count++; return Task.CompletedTask; });

        count.Should().Be(1);
    }
}
