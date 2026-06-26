using Microsoft.Extensions.DependencyInjection;

namespace Vostra.Result.AspNetCore;

/// <summary>DI registration for Vostra.Result HTTP mapping.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers a <see cref="VostraResultsOptions"/> singleton, optionally configured. Calling this is
    /// optional — <c>ToHttpResponse</c> falls back to <see cref="VostraResultsOptions.Default"/> when absent.
    /// </summary>
    public static IServiceCollection AddVostraResults(
        this IServiceCollection services,
        Action<VostraResultsOptions>? configure = null)
    {
        var options = new VostraResultsOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);
        return services;
    }
}
