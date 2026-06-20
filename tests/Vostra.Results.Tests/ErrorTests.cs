namespace Vostra.Results.Tests;

public class ErrorTests
{
    [Fact]
    public void Builtin_error_carries_type_and_default_code()
    {
        var error = new NotFoundError("Order 5 not found");

        error.Type.Should().Be(ErrorType.NotFound);
        error.Code.Should().Be("General.NotFound");
        error.Message.Should().Be("Order 5 not found");
        error.CausedBy.Should().BeNull();
        error.Metadata.Should().BeNull();
    }

    [Fact]
    public void Errors_of_same_type_code_and_message_are_equal()
    {
        ErrorBase a = new NotFoundError("missing", code: "Order.NotFound");
        ErrorBase b = new NotFoundError("missing", code: "Order.NotFound");

        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void Errors_of_different_subtype_are_not_equal()
    {
        ErrorBase a = new NotFoundError("x", code: "Same");
        ErrorBase b = new ConflictError("x", code: "Same");

        a.Should().NotBe(b);
    }

    [Fact]
    public void CausedBy_and_metadata_are_opt_in()
    {
        var ex = new InvalidOperationException("boom");
        var meta = new Dictionary<string, object?> { ["orderId"] = 5 };

        var error = new Error("failed", causedBy: ex, metadata: meta);

        error.CausedBy.Should().BeSameAs(ex);
        error.Metadata.Should().ContainKey("orderId");
    }
}
