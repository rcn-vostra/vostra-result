namespace Vostra.Result.Tests;

public class AggregationTests
{
    [Fact]
    public void Combine_nongeneric_is_success_when_all_succeed()
    {
        Result.Combine(Result.Ok(), Result.Ok()).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Combine_nongeneric_accumulates_all_errors()
    {
        var combined = Result.Combine(
            Result.Failure(new ValidationError("a")),
            Result.Ok(),
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
            Result.Ok(),
            Result.Failure(new ValidationError("a")),
            Result.Failure(new ValidationError("b")),
        };

        var combined = results.Combine();

        combined.IsError.Should().BeTrue();
        combined.Errors.Should().HaveCount(2);
    }

    [Fact]
    public async Task SelectResultsAsync_preserves_both_successes_and_failures()
    {
        var items = new[] { 1, 2, 3, 4 };

        IReadOnlyList<Result<int>> outcomes = await items.SelectResultsAsync(
            i => Task.FromResult(i % 2 == 0
                ? (Result<int>)i
                : (Result<int>)new ValidationError($"odd {i}")),
            maxConcurrency: 2);

        outcomes.Should().HaveCount(4);
        outcomes.Count(r => r.IsSuccess).Should().Be(2);
        outcomes.Count(r => r.IsError).Should().Be(2);
        outcomes[1].Match(v => v, _ => -1).Should().Be(2);   // input order preserved
        outcomes[0].IsError.Should().BeTrue();
    }

    [Fact]
    public async Task SelectResultsAsync_returns_results_in_source_order()
    {
        var items = Enumerable.Range(0, 6).ToArray();

        // Later items finish first; result order must still match input order.
        IReadOnlyList<Result<int>> outcomes = await items.SelectResultsAsync(
            async i => { await Task.Delay((6 - i) * 5); return (Result<int>)i; },
            maxConcurrency: 6);

        outcomes.Select(r => r.Match(v => v, _ => -1)).Should().Equal(0, 1, 2, 3, 4, 5);
    }

    [Fact]
    public async Task SelectResultsAsync_respects_the_concurrency_cap()
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

        await Enumerable.Range(0, 8).SelectResultsAsync(Track, maxConcurrency: 3);

        peak.Should().BeLessThanOrEqualTo(3);
    }

    [Fact]
    public async Task SelectResultsAsync_throws_when_maxConcurrency_below_one()
    {
        Func<Task> act = () => Enumerable.Range(0, 3)
            .SelectResultsAsync(i => Task.FromResult<Result<int>>(i), maxConcurrency: 0);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task SelectResultsAsync_returns_empty_for_empty_source()
    {
        IReadOnlyList<Result<int>> outcomes = await Array.Empty<int>()
            .SelectResultsAsync(i => Task.FromResult<Result<int>>(i), maxConcurrency: 2);

        outcomes.Should().BeEmpty();
    }

    [Fact]
    public async Task SelectResultsAsync_observes_cancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Func<Task> act = () => Enumerable.Range(0, 3)
            .SelectResultsAsync(i => Task.FromResult<Result<int>>(i), maxConcurrency: 2, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task SelectResultsAsync_outcomes_zip_back_to_inputs_in_order()
    {
        var items = new[] { 1, 2, 3 };

        IReadOnlyList<Result<int>> outcomes = await items
            .SelectResultsAsync(i => Task.FromResult<Result<int>>(i * 10), maxConcurrency: 2);

        var paired = items.Zip(outcomes, (item, outcome) => (item, value: outcome.Match(v => v, _ => -1)));
        paired.Should().Equal((1, 10), (2, 20), (3, 30));
    }

    [Fact]
    public async Task SelectAsync_still_collapses_to_errors_discarding_successes()
    {
        var items = new[] { 1, 2, 3 };

        Result<IReadOnlyList<int>> result = await items.SelectAsync(
            i => Task.FromResult(i == 2
                ? (Result<int>)new NotFoundError("no 2")
                : (Result<int>)i),
            maxConcurrency: 2);

        result.IsError.Should().BeTrue();
        result.Errors.Should().HaveCount(1);   // the two successes (1, 3) are gone
    }
}
