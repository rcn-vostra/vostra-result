namespace Vostra.Results.Tests;

public class ResultErrorFactoriesTests
{
    public static TheoryData<ErrorBase, ErrorBase> FactoryAndTwin() => new()
    {
        { Result.ValidationError("m"), new ValidationError("m") },
        { Result.NotFoundError("m"), new NotFoundError("m") },
        { Result.ConflictError("m"), new ConflictError("m") },
        { Result.AlreadyExistsError("m"), new AlreadyExistsError("m") },
        { Result.UnauthorizedError("m"), new UnauthorizedError("m") },
        { Result.ForbiddenError("m"), new ForbiddenError("m") },
        { Result.Failure("m"), new Error("m") },
    };

    [Theory]
    [MemberData(nameof(FactoryAndTwin))]
    public void Factory_equals_hand_constructed_twin(ErrorBase factory, ErrorBase twin)
    {
        // Same concrete type, kind, default code, and message as the `new XxxError(...)` form.
        factory.GetType().Should().Be(twin.GetType());
        factory.Type.Should().Be(twin.Type);
        factory.Code.Should().Be(twin.Code);
        factory.Message.Should().Be(twin.Message);
        factory.Should().Be(twin);
    }

    [Fact]
    public void Default_code_is_the_kind_fallback_and_custom_code_is_honored()
    {
        Result.NotFoundError("missing").Code.Should().Be("General.NotFound");
        Result.NotFoundError("missing", "Order.NotFound").Code.Should().Be("Order.NotFound");
    }

    [Fact]
    public void CausedBy_and_metadata_flow_through()
    {
        var ex = new InvalidOperationException("boom");
        var meta = new Dictionary<string, object?> { ["orderId"] = 5 };

        var error = Result.NotFoundError("missing", "Order.NotFound", ex, meta);

        error.CausedBy.Should().BeSameAs(ex);
        error.Metadata.Should().ContainKey("orderId");
    }

    // --- usage: the factory used at a real call site, implicitly converting to the result type ---

    private sealed record Order(int Id);

    private static Result<Order> GetOrder(int id) =>
        id == 7
            ? new Order(id)
            : Result.NotFoundError($"Order {id} not found", "Order.NotFound");

    [Fact]
    public void Factory_converts_to_a_failed_result_of_T_at_the_call_site()
    {
        GetOrder(7).Match(o => o.Id, _ => -1).Should().Be(7);

        Result<Order> missing = GetOrder(9);
        missing.IsError.Should().BeTrue();
        missing.FirstError.Should().BeOfType<NotFoundError>();
        missing.FirstError.Code.Should().Be("Order.NotFound");
    }

    [Fact]
    public void Factory_converts_to_a_failed_nongeneric_result()
    {
        Result result = Result.ValidationError("name is required", code: "Name.Required");

        result.IsError.Should().BeTrue();
        result.FirstError.Should().BeOfType<ValidationError>();
        result.FirstError.Type.Should().Be(ErrorType.Validation);
        result.FirstError.Code.Should().Be("Name.Required");
    }

    [Fact]
    public void ValidationError_field_parameter_populates_the_field_metadata_key()
    {
        var error = Result.ValidationError("Email is invalid", field: "email", code: "Email.Invalid");

        error.Code.Should().Be("Email.Invalid");
        error.Metadata.Should().ContainKey(ErrorBase.FieldMetadataKey);
        error.Metadata![ErrorBase.FieldMetadataKey].Should().Be("email");
    }
}
