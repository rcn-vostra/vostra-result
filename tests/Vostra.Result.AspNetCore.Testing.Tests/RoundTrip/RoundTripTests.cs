namespace Vostra.Result.Testing.Tests.RoundTrip;

public class RoundTripTests : IClassFixture<TestApiHost>
{
    private readonly TestApiHost _host;

    public RoundTripTests(TestApiHost host) => _host = host;

    [Fact]
    public async Task Get_existing_returns_value()
    {
        var product = await _host.Api.Get<Product>("/7").ShouldBeSuccess();

        product.Should().Be(new Product(7, "Widget"));
    }

    [Fact]
    public async Task Get_missing_rebuilds_typed_NotFound_with_code()
    {
        await _host.Api.Get<Product>("/9").ShouldHaveError("Product.NotFound");
    }

    [Fact]
    public async Task Get_missing_is_typed_NotFound()
    {
        var result = await _host.Api.Get<Product>("/9");

        result.FirstError.Should().BeOfType<NotFoundError>();
    }

    [Fact]
    public async Task Post_created_returns_value_with_Created_kind()
    {
        var result = await _host.Api.Post<Product>("", new Product(0, "New"));

        result.SuccessKind.Should().Be(SuccessKind.Created);
        result.ShouldBeSuccess().Name.Should().Be("New");
    }

    [Fact]
    public async Task Post_invalid_rebuilds_validation_error()
    {
        await _host.Api.Post<Product>("", new Product(0, "")).ShouldBeValidation();
    }

    [Fact]
    public async Task GetList_returns_items_and_pagination()
    {
        var page = await _host.Api.GetList<Product>("").ShouldBeSuccess();

        page.Items.Should().HaveCount(2);
        page.Pagination.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task Delete_existing_is_success()
    {
        await _host.Api.Delete("/7").ShouldBeSuccess();
    }

    [Fact]
    public async Task Delete_missing_rebuilds_typed_error()
    {
        await _host.Api.Delete("/9").ShouldHaveError("Product.NotFound");
    }
}
