namespace Vostra.Result.Tests;

public class AsyncTerminalsTests
{
    private static Task<Result<int>> OkAsync(int v) => Task.FromResult<Result<int>>(v);
    private static Task<Result<int>> FailAsync(string code) => Task.FromResult<Result<int>>(new ValidationError("bad", code: code));

    [Fact]
    public async Task Match_on_async_receiver_runs_the_matching_branch()
    {
        (await OkAsync(7).Match(v => v * 2, _ => -1)).Should().Be(14);
        (await FailAsync("X").Match(v => v * 2, errs => -errs.Count)).Should().Be(-1);
    }

    [Fact]
    public async Task MatchFirst_on_async_receiver_sees_the_first_error()
    {
        (await FailAsync("X.Code").MatchFirst(_ => "ok", e => e.Code)).Should().Be("X.Code");
    }

    [Fact]
    public async Task Switch_on_async_receiver_runs_the_matching_action()
    {
        int seen = 0;
        await OkAsync(5).Switch(v => seen = v, _ => seen = -1);
        seen.Should().Be(5);

        await FailAsync("X").Switch(v => seen = v, _ => seen = -99);
        seen.Should().Be(-99);
    }

    [Fact]
    public async Task SwitchFirst_on_async_receiver_sees_the_first_error()
    {
        string? code = null;
        await FailAsync("X.Code").SwitchFirst(_ => { }, e => code = e.Code);
        code.Should().Be("X.Code");
    }

    [Fact]
    public async Task GetValueOr_on_async_receiver_returns_value_or_fallback()
    {
        (await OkAsync(7).GetValueOr(0)).Should().Be(7);
        (await FailAsync("X").GetValueOr(42)).Should().Be(42);
        (await FailAsync("X").GetValueOr(errs => errs.Count)).Should().Be(1);
    }

    [Fact]
    public async Task Chain_terminates_fluently_without_a_wrapping_await()
    {
        // the headline: Then/Map/Match in one top-to-bottom expression, one leading await
        string label = await OkAsync(3)
            .Then(v => (Result<int>)(v + 1))
            .Map(v => v * 10)
            .Match(v => $"#{v}", errs => errs[0].Message);

        label.Should().Be("#40");
    }

    [Fact]
    public async Task Union2_async_match_and_switch()
    {
        Task<Result<int, string>> arm1 = Task.FromResult<Result<int, string>>(5);
        Task<Result<int, string>> arm2 = Task.FromResult<Result<int, string>>("hi");

        (await arm1.Match(i => $"int:{i}", s => $"str:{s}", _ => "err")).Should().Be("int:5");
        (await arm2.Match(i => $"int:{i}", s => $"str:{s}", _ => "err")).Should().Be("str:hi");

        string seen = "";
        await arm2.Switch(_ => seen = "int", s => seen = s, _ => seen = "err");
        seen.Should().Be("hi");
    }

    [Fact]
    public async Task Union3_async_match_and_switch()
    {
        Task<Result<int, string, bool>> arm3 = Task.FromResult<Result<int, string, bool>>(true);

        (await arm3.Match(_ => "int", _ => "str", b => $"bool:{b}", _ => "err")).Should().Be("bool:True");

        string seen = "";
        await arm3.Switch(_ => seen = "int", _ => seen = "str", b => seen = $"bool:{b}", _ => seen = "err");
        seen.Should().Be("bool:True");
    }
}
