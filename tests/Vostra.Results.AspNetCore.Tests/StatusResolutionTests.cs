namespace Vostra.Results.AspNetCore.Tests;

public class StatusResolutionTests
{
    [Theory]
    [InlineData(ErrorType.Validation, 400)]
    [InlineData(ErrorType.Unauthorized, 401)]
    [InlineData(ErrorType.Forbidden, 403)]
    [InlineData(ErrorType.NotFound, 404)]
    [InlineData(ErrorType.Conflict, 409)]
    [InlineData(ErrorType.Unexpected, 500)]
    public void Default_map_covers_every_error_type(ErrorType type, int expected)
    {
        DefaultStatusMap.ForType(type).Should().Be(expected);
    }

    [Fact]
    public void Unconfigured_options_use_defaults()
    {
        ErrorStatusResolver.Resolve(new NotFoundError("x"), VostraResultsOptions.Default).Should().Be(404);
    }

    [Fact]
    public void MapStatus_overrides_a_whole_type()
    {
        var opts = new VostraResultsOptions().MapStatus(ErrorType.Conflict, 422);
        ErrorStatusResolver.Resolve(new ConflictError("x"), opts).Should().Be(422);
    }

    [Fact]
    public void MapStatusForCode_beats_the_type_map()
    {
        var opts = new VostraResultsOptions()
            .MapStatus(ErrorType.Conflict, 422)
            .MapStatusForCode("Order.Locked", 423);
        var error = new ConflictError("locked", code: "Order.Locked");
        ErrorStatusResolver.Resolve(error, opts).Should().Be(423);
    }

    [Fact]
    public void New_subclass_maps_through_its_error_type_with_no_config()
    {
        ErrorStatusResolver.Resolve(new CustomTeapotError(), VostraResultsOptions.Default).Should().Be(404);
    }

    private sealed class CustomTeapotError : ErrorBase
    {
        public CustomTeapotError() : base("Custom.Teapot", "I'm a teapot", ErrorType.NotFound) { }
        protected override ErrorBase CloneWith(string code, string message, Exception? causedBy, IReadOnlyDictionary<string, object?>? metadata)
            => new CustomTeapotError();
    }
}
