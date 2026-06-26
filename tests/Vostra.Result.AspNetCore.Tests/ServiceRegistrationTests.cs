using Microsoft.Extensions.DependencyInjection;

namespace Vostra.Result.AspNetCore.Tests;

public class ServiceRegistrationTests
{
    [Fact]
    public void AddVostraResults_registers_configured_options()
    {
        var provider = new ServiceCollection()
            .AddVostraResults(o => o.MapStatus(ErrorType.Conflict, 422))
            .BuildServiceProvider();

        var options = provider.GetRequiredService<VostraResultsOptions>();
        ErrorStatusResolver.Resolve(new ConflictError("x"), options).Should().Be(422);
    }

    [Fact]
    public void AddVostraResults_without_configure_registers_default_behavior()
    {
        var provider = new ServiceCollection().AddVostraResults().BuildServiceProvider();

        var options = provider.GetRequiredService<VostraResultsOptions>();
        ErrorStatusResolver.Resolve(new NotFoundError("x"), options).Should().Be(404);
    }
}
