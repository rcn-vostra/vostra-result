namespace Vostra.Results.Testing.Tests;

public class PagedListTests
{
    [Fact]
    public void PagedList_carries_items_and_pagination()
    {
        var page = new PagedList<string>(new[] { "a", "b" }, new Pagination(1, 20, 2));

        page.Items.Should().Equal("a", "b");
        page.Pagination.TotalCount.Should().Be(2);
    }
}
