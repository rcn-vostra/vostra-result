using System.Text.Json;

namespace Vostra.Results.AspNetCore.Tests;

public class EnvelopeTests
{
    [Theory]
    [InlineData(20, 137, 7)]   // 137/20 -> 7 pages
    [InlineData(20, 140, 7)]   // exact multiple
    [InlineData(20, 0, 0)]     // empty
    [InlineData(0, 137, 0)]    // pageSize 0 -> no div-by-zero
    public void Pagination_TotalPages_is_computed(int pageSize, long total, int expected)
    {
        new Pagination(1, pageSize, total).TotalPages.Should().Be(expected);
    }

    [Fact]
    public void SuccessEnvelope_serializes_camelCase_and_always_writes_data_even_for_default()
    {
        var json = JsonSerializer.Serialize(new SuccessEnvelope<int> { OperationId = "op1", Data = 0 });
        json.Should().Contain("\"operationId\":\"op1\"");
        json.Should().Contain("\"data\":0");
    }

    [Fact]
    public void ListEnvelope_serializes_data_and_pagination()
    {
        var env = new ListEnvelope<int>
        {
            OperationId = "op2",
            Data = new[] { 1, 2 },
            Pagination = new Pagination(1, 20, 2),
        };
        var json = JsonSerializer.Serialize(env);
        json.Should().Contain("\"operationId\":\"op2\"");
        json.Should().Contain("\"data\":[1,2]");
        json.Should().Contain("\"pagination\":");
        json.Should().Contain("\"totalCount\":2");
    }
}
