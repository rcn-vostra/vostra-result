namespace Vostra.Results.Tests;

public class AggregationTests
{
    [Fact]
    public void Combine_nongeneric_is_success_when_all_succeed()
    {
        Result.Combine(Result.Success, Result.Success).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Combine_nongeneric_accumulates_all_errors()
    {
        var combined = Result.Combine(
            Result.Failure(new ValidationError("a")),
            Result.Success,
            Result.Failure(new ValidationError("b")));

        combined.IsError.Should().BeTrue();
        combined.Errors.Should().HaveCount(2);
    }

    [Fact]
    public void Combine_generic_collects_values_in_order()
    {
        Result<int> a = 1, b = 2, c = 3;

        var combined = Result.Combine(a, b, c);

        combined.IsSuccess.Should().BeTrue();
        combined.Match(v => v, _ => Array.Empty<int>()).Should().Equal(1, 2, 3);
    }

    [Fact]
    public void Combine_generic_accumulates_errors()
    {
        Result<int> a = 1;
        Result<int> b = new NotFoundError("b");
        Result<int> c = new ConflictError("c");

        var combined = Result.Combine(a, b, c);

        combined.IsError.Should().BeTrue();
        combined.Errors.Should().HaveCount(2);
    }

    [Fact]
    public async Task SelectAsync_projects_all_items_and_collects_results()
    {
        var items = new[] { 1, 2, 3 };

        Result<IReadOnlyList<int>> result =
            await items.SelectAsync(i => Task.FromResult<Result<int>>(i * 2), maxConcurrency: 2);

        result.IsSuccess.Should().BeTrue();
        result.Match(v => v, _ => Array.Empty<int>()).Should().Equal(2, 4, 6);
    }

    [Fact]
    public async Task SelectAsync_respects_the_concurrency_cap()
    {
        var current = 0;
        var peak = 0;
        var gate = new object();

        async Task<Result<int>> Track(int i)
        {
            lock (gate) { current++; peak = Math.Max(peak, current); }
            await Task.Delay(20);
            lock (gate) { current--; }
            return i;
        }

        await Enumerable.Range(0, 8).SelectAsync(Track, maxConcurrency: 3);

        peak.Should().BeLessThanOrEqualTo(3);
    }

    [Fact]
    public void Combine_over_enumerable_of_generic_collects_in_order()
    {
        IEnumerable<Result<int>> results = new[] { Result.Ok(1), Result.Ok(2), Result.Ok(3) };

        var combined = results.Combine();

        combined.IsSuccess.Should().BeTrue();
        combined.Match(v => v, _ => Array.Empty<int>()).Should().Equal(1, 2, 3);
    }

    [Fact]
    public void Combine_over_enumerable_of_nongeneric_accumulates_errors()
    {
        IEnumerable<Result> results = new[]
        {
            Result.Success,
            Result.Failure(new ValidationError("a")),
            Result.Failure(new ValidationError("b")),
        };

        var combined = results.Combine();

        combined.IsError.Should().BeTrue();
        combined.Errors.Should().HaveCount(2);
    }
}
