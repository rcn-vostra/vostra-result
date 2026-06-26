namespace Vostra.Result.Tests;

public class ErrorRewrapTests
{
    [Fact]
    public void Prefix_preserves_concrete_type_and_changes_message()
    {
        ErrorBase original = new NotFoundError("order 5", code: "Order.NotFound");
        var prefixed = original.Prefix("shipping");

        prefixed.Should().BeOfType<NotFoundError>();
        prefixed.Message.Should().Be("shipping: order 5");
        prefixed.Code.Should().Be("Order.NotFound");
        prefixed.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public void WithCode_preserves_type_and_message()
    {
        ErrorBase original = new ConflictError("dup");
        var recoded = original.WithCode("Order.Duplicate");

        recoded.Should().BeOfType<ConflictError>();
        recoded.Code.Should().Be("Order.Duplicate");
        recoded.Message.Should().Be("dup");
    }

    [Fact]
    public void WithCausedBy_attaches_exception_and_keeps_type()
    {
        var ex = new InvalidOperationException("boom");
        ErrorBase original = new Error("failed");
        var wrapped = original.WithCausedBy(ex);

        wrapped.Should().BeOfType<Error>();
        wrapped.CausedBy.Should().BeSameAs(ex);
    }

    [Fact]
    public void WithMetadata_attaches_metadata_preserving_type_code_and_message()
    {
        ErrorBase original = new ValidationError("bad input", code: "Order.Invalid");
        var meta = new Dictionary<string, object?> { ["field"] = "email" };

        var withMeta = original.WithMetadata(meta);

        withMeta.Should().BeOfType<ValidationError>();
        withMeta.Metadata.Should().ContainKey("field");
        withMeta.Code.Should().Be("Order.Invalid");
        withMeta.Message.Should().Be("bad input");
        withMeta.Type.Should().Be(ErrorType.Validation);
    }
}
