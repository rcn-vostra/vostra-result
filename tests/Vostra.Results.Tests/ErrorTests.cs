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

    [Fact]
    public void ValidationError_without_a_field_leaves_metadata_null()
    {
        new ValidationError("bad").Metadata.Should().BeNull();
    }

    [Fact]
    public void ValidationError_field_merges_with_metadata_and_survives_a_clone()
    {
        var meta = new Dictionary<string, object?> { ["traceId"] = "abc" };
        var error = new ValidationError("bad", field: "email", metadata: meta);

        error.Metadata.Should().ContainKey("traceId");
        error.Metadata![ErrorBase.FieldMetadataKey].Should().Be("email");

        // CloneWith carries the (field-bearing) metadata through, so With* preserve the field.
        var recoded = error.WithCode("Email.Invalid");
        recoded.Code.Should().Be("Email.Invalid");
        recoded.Metadata!["traceId"].Should().Be("abc");
        recoded.Metadata[ErrorBase.FieldMetadataKey].Should().Be("email");
    }
}
