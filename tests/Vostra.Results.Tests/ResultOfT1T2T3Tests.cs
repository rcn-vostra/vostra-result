namespace Vostra.Results.Tests;

public class ResultOfT1T2T3Tests
{
    private static Result<int, string, bool> Produce(int which) => which switch
    {
        1 => 42,
        2 => "hello",
        3 => true,
        _ => new NotFoundError("missing"),
    };

    [Fact]
    public void Each_arm_converts_implicitly_to_success()
    {
        ((Result<int, string, bool>)42).Index.Should().Be(1);
        ((Result<int, string, bool>)"hello").Index.Should().Be(2);
        ((Result<int, string, bool>)true).Index.Should().Be(3);
    }

    [Fact]
    public void Error_converts_implicitly_to_failure()
    {
        Result<int, string, bool> result = new NotFoundError("missing");

        result.IsError.Should().BeTrue();
        result.Index.Should().Be(0);
        result.FirstError.Should().BeOfType<NotFoundError>();
    }

    [Fact]
    public void Error_array_and_list_convert_to_failure()
    {
        Result<int, string, bool> fromArray = new ErrorBase[] { new NotFoundError("a"), new ConflictError("b") };
        Result<int, string, bool> fromList = new List<ErrorBase> { new ValidationError("a") };

        fromArray.Errors.Should().HaveCount(2);
        fromList.FirstError.Should().BeOfType<ValidationError>();
    }

    [Fact]
    public void Default_result_is_faulted_not_a_success()
    {
        Result<int, string, bool> result = default;

        result.IsError.Should().BeTrue();
        result.Index.Should().Be(0);
        result.FirstError.Code.Should().Be("Result.Uninitialized");
    }

    [Fact]
    public void Match_routes_to_the_active_arm()
    {
        Produce(1).Match(i => $"int:{i}", s => $"str:{s}", b => $"bool:{b}", e => "err").Should().Be("int:42");
        Produce(2).Match(i => $"int:{i}", s => $"str:{s}", b => $"bool:{b}", e => "err").Should().Be("str:hello");
        Produce(3).Match(i => $"int:{i}", s => $"str:{s}", b => $"bool:{b}", e => "err").Should().Be("bool:True");
        Produce(4).Match(i => "int", s => "str", b => "bool", e => e[0].Code).Should().Be("General.NotFound");
    }

    [Fact]
    public void Switch_invokes_the_active_action()
    {
        string? hit = null;
        Produce(3).Switch(i => hit = "int", s => hit = "str", b => hit = "bool", e => hit = "err");
        hit.Should().Be("bool");

        Produce(4).Switch(i => hit = "int", s => hit = "str", b => hit = "bool", e => hit = "err");
        hit.Should().Be("err");
    }

    [Fact]
    public void Match_with_null_delegate_for_active_arm_throws()
    {
        Func<bool, string> onT3 = null!;
        var act = () => Produce(3).Match(i => "int", s => "str", onT3, e => "err");
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Explicit_arm_factories_disambiguate_same_typed_arms()
    {
        Result<int, int, int> a = Result<int, int, int>.First(0);
        Result<int, int, int> b = Result<int, int, int>.Second(0);
        Result<int, int, int> c = Result<int, int, int>.Third(0);

        a.Index.Should().Be(1);
        b.Index.Should().Be(2);
        c.Index.Should().Be(3);
        a.Should().NotBe(b);
        b.Should().NotBe(c);
    }

    [Fact]
    public void Same_arm_equal_values_are_equal_and_failures_compare()
    {
        Result<int, string, bool> a = "x";
        Result<int, string, bool> b = "x";
        a.Should().Be(b);

        Result<int, string, bool> e1 = new NotFoundError("x", code: "C");
        Result<int, string, bool> e2 = new NotFoundError("x", code: "C");
        e1.Should().Be(e2);
    }

    [Fact]
    public void Equal_results_have_equal_hash_codes()
    {
        Result<int, string, bool> a = true;
        Result<int, string, bool> b = true;
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void Null_reference_success_does_not_throw_on_hashcode()
    {
        Result<int, string?, bool> result = (string?)null;

        result.IsSuccess.Should().BeTrue();
        result.Index.Should().Be(2);
        var act = () => result.GetHashCode();
        act.Should().NotThrow();
    }

    [Fact]
    public void Switch_with_null_delegate_for_active_arm_throws()
    {
        Action<bool> onT3 = null!;
        var act = () => Produce(3).Switch(i => { }, s => { }, onT3, e => { });
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Error_arm_receives_the_full_error_list()
    {
        Result<int, string, bool> result = new ErrorBase[] { new NotFoundError("a"), new ConflictError("b") };

        result.Match(i => 0, s => 0, b => 0, e => e.Count).Should().Be(2);
    }

    [Fact]
    public void ToString_distinguishes_success_and_error()
    {
        Produce(3).ToString().Should().Contain("Success");
        Produce(4).ToString().Should().Contain("Error");
    }
}
