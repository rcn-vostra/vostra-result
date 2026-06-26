namespace Vostra.Result.Tests;

public class SuccessKindTests
{
    [Fact]
    public void Plain_success_is_Ok()
    {
        Result<int> r = 5;
        r.SuccessKind.Should().Be(SuccessKind.Ok);
    }

    [Fact]
    public void Created_factory_marks_success_as_Created()
    {
        Result<int> r = Result.Created(5);
        r.IsSuccess.Should().BeTrue();
        r.SuccessKind.Should().Be(SuccessKind.Created);
        r.Match(v => v, _ => -1).Should().Be(5);
    }

    [Fact]
    public void Nongeneric_created_is_a_created_success()
    {
        Result r = Result.Created();
        r.IsSuccess.Should().BeTrue();
        r.SuccessKind.Should().Be(SuccessKind.Created);
    }

    [Fact]
    public void Ok_and_Created_with_same_value_are_not_equal()
    {
        Result<int> ok = 5;
        Result<int> created = Result.Created(5);
        ok.Should().NotBe(created);
    }
}
