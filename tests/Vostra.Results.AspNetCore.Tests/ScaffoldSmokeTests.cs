namespace Vostra.Results.AspNetCore.Tests;

public class ScaffoldSmokeTests
{
    [Fact]
    public void Core_types_are_referenced()
    {
        Result<int> ok = 7;
        ok.IsSuccess.Should().BeTrue();
    }
}
