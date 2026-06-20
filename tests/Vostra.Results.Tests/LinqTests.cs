namespace Vostra.Results.Tests;

public class LinqTests
{
    private static Result<int> GetOrder(int id) => id > 0 ? id : new NotFoundError("no order");
    private static Result<int> GetPayment(int orderId) => orderId * 10;
    private static Task<Result<int>> GetOrderAsync(int id) => Task.FromResult(GetOrder(id));
    private static Task<Result<int>> GetPaymentAsync(int orderId) => Task.FromResult(GetPayment(orderId));

    [Fact]
    public void Select_projects_success()
    {
        Result<int> q = from o in GetOrder(2) select o + 1;
        q.Should().Be(Result.Ok(3));
    }

    [Fact]
    public void SelectMany_composes_two_results()
    {
        Result<int> total =
            from o in GetOrder(2)
            from p in GetPayment(o)
            select o + p;

        total.Should().Be(Result.Ok(2 + 20));
    }

    [Fact]
    public void SelectMany_short_circuits_on_first_error()
    {
        Result<int> total =
            from o in GetOrder(-1)
            from p in GetPayment(o)
            select o + p;

        total.IsError.Should().BeTrue();
        total.FirstError.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public void Where_failure_is_a_validation_error()
    {
        Result<int> q = from o in GetOrder(2) where o > 100 select o;

        q.IsError.Should().BeTrue();
        q.FirstError.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task SelectMany_works_over_task_of_result()
    {
        Result<int> total = await (
            from o in GetOrderAsync(2)
            from p in GetPaymentAsync(o)
            select o + p);

        total.Should().Be(Result.Ok(2 + 20));
    }
}
