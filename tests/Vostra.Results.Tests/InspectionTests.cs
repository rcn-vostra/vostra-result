namespace Vostra.Results.Tests;

public class InspectionTests
{
    private static Result<int> Find(int id) =>
        id == 1 ? 100 : new NotFoundError($"id {id}");

    [Fact]
    public void Match_runs_the_success_branch()
    {
        var label = Find(1).Match(
            onOk: v => $"value {v}",
            onErr: errors => errors[0].Message);

        label.Should().Be("value 100");
    }

    [Fact]
    public void Match_runs_the_error_branch_without_touching_value()
    {
        var label = Find(2).Match(
            onOk: v => $"value {v}",
            onErr: errors => errors[0].Message);

        label.Should().Be("id 2");
    }

    [Fact]
    public void MatchFirst_passes_the_first_error()
    {
        var type = Find(2).MatchFirst(_ => ErrorType.Unexpected, e => e.Type);

        type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public void Switch_invokes_the_matching_action()
    {
        var seen = "";
        Find(1).Switch(v => seen = $"ok{v}", _ => seen = "err");
        seen.Should().Be("ok100");
    }

    [Fact]
    public void TryGetValue_is_true_on_success()
    {
        Find(1).TryGetValue(out var value).Should().BeTrue();
        value.Should().Be(100);
    }

    [Fact]
    public void TryGetValue_is_false_on_error()
    {
        Find(2).TryGetValue(out _).Should().BeFalse();
    }

    [Fact]
    public void TryGetErrors_is_true_on_error()
    {
        Find(2).TryGetErrors(out var errors).Should().BeTrue();
        errors!.Should().ContainSingle();
    }

    [Fact]
    public void GetValueOr_returns_fallback_on_error()
    {
        Find(2).GetValueOr(-1).Should().Be(-1);
        Find(1).GetValueOr(-1).Should().Be(100);
    }
}
