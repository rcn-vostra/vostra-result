namespace Vostra.Results.Tests;

public class ResultOfT1T2Tests
{
    private static Result<int, string> Produce(int which) => which switch
    {
        1 => 42,
        2 => "hello",
        _ => new NotFoundError("missing"),
    };

    [Fact]
    public void First_arm_converts_implicitly_to_success()
    {
        Result<int, string> result = 42;

        result.IsSuccess.Should().BeTrue();
        result.IsError.Should().BeFalse();
        result.Index.Should().Be(1);
    }

    [Fact]
    public void Second_arm_converts_implicitly_to_success()
    {
        Result<int, string> result = "hello";

        result.IsSuccess.Should().BeTrue();
        result.Index.Should().Be(2);
    }

    [Fact]
    public void Error_converts_implicitly_to_failure()
    {
        Result<int, string> result = new NotFoundError("missing");

        result.IsError.Should().BeTrue();
        result.Index.Should().Be(0);
        result.FirstError.Should().BeOfType<NotFoundError>();
        result.Errors.Should().ContainSingle();
    }

    [Fact]
    public void Error_array_converts_implicitly_to_failure()
    {
        Result<int, string> result = new ErrorBase[] { new NotFoundError("a"), new ConflictError("b") };

        result.IsError.Should().BeTrue();
        result.Errors.Should().HaveCount(2);
    }

    [Fact]
    public void Error_list_converts_implicitly_to_failure()
    {
        Result<int, string> result = new List<ErrorBase> { new ValidationError("a") };

        result.IsError.Should().BeTrue();
        result.FirstError.Should().BeOfType<ValidationError>();
    }

    [Fact]
    public void Default_result_is_faulted_not_a_success()
    {
        Result<int, string> result = default;

        result.IsError.Should().BeTrue();
        result.IsSuccess.Should().BeFalse();
        result.Index.Should().Be(0);
        result.FirstError.Code.Should().Be("Result.Uninitialized");
    }

    [Fact]
    public void Match_routes_to_the_active_arm()
    {
        Produce(1).Match(i => $"int:{i}", s => $"str:{s}", e => "err").Should().Be("int:42");
        Produce(2).Match(i => $"int:{i}", s => $"str:{s}", e => "err").Should().Be("str:hello");
        Produce(3).Match(i => $"int:{i}", s => $"str:{s}", e => e[0].Code).Should().Be("General.NotFound");
    }

    [Fact]
    public void Switch_invokes_the_active_action()
    {
        string? hit = null;
        Produce(2).Switch(i => hit = "int", s => hit = "str", e => hit = "err");
        hit.Should().Be("str");

        Produce(3).Switch(i => hit = "int", s => hit = "str", e => hit = "err");
        hit.Should().Be("err");
    }

    [Fact]
    public void Same_arm_equal_values_are_equal()
    {
        Result<int, string> a = 7;
        Result<int, string> b = 7;

        a.Should().Be(b);
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void Explicit_arm_factories_disambiguate_same_typed_arms()
    {
        // With T1 == T2, implicit conversion is ambiguous; First/Second select the arm explicitly.
        Result<int, int> a = Result<int, int>.First(0);
        Result<int, int> b = Result<int, int>.Second(0);

        a.Index.Should().Be(1);
        b.Index.Should().Be(2);
        a.Should().NotBe(b);
    }

    [Fact]
    public void Equal_failures_are_equal()
    {
        Result<int, string> a = new NotFoundError("x", code: "C");
        Result<int, string> b = new NotFoundError("x", code: "C");

        a.Should().Be(b);
    }

    [Fact]
    public void Equal_results_have_equal_hash_codes()
    {
        Result<int, string> a = "x";
        Result<int, string> b = "x";
        a.GetHashCode().Should().Be(b.GetHashCode());

        Result<int, string> e1 = new NotFoundError("x", code: "C");
        Result<int, string> e2 = new NotFoundError("x", code: "C");
        e1.GetHashCode().Should().Be(e2.GetHashCode());
    }

    [Fact]
    public void Error_arm_receives_the_full_error_list()
    {
        Result<int, string> result = new ErrorBase[] { new NotFoundError("a"), new ConflictError("b") };

        result.Match(i => 0, s => 0, e => e.Count).Should().Be(2);
    }

    [Fact]
    public void ToString_distinguishes_success_and_error()
    {
        Produce(1).ToString().Should().Contain("Success");
        Produce(3).ToString().Should().Contain("Error");
    }

    [Fact]
    public void Null_reference_success_does_not_throw_on_hashcode()
    {
        Result<string?, int> result = (string?)null;

        result.IsSuccess.Should().BeTrue();
        result.Index.Should().Be(1);
        var act = () => result.GetHashCode();
        act.Should().NotThrow();
    }
}
