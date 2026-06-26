using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Vostra.Result.AspNetCore.Tests;

/// <summary>Executes an <see cref="IResult"/> against a real <see cref="DefaultHttpContext"/> and captures it.</summary>
internal static class HttpResultHarness
{
    public static async Task<ExecutedResponse> Execute(IResult result, IServiceProvider? services = null)
    {
        var ctx = new DefaultHttpContext();
        ctx.RequestServices = services ?? new ServiceCollection().AddLogging().BuildServiceProvider();
        ctx.TraceIdentifier = "trace-abc";
        var body = new MemoryStream();
        ctx.Response.Body = body;

        await result.ExecuteAsync(ctx);

        body.Position = 0;
        var text = await new StreamReader(body).ReadToEndAsync();
        return new ExecutedResponse(ctx.Response.StatusCode, ctx.Response.ContentType, text);
    }
}

internal sealed record ExecutedResponse(int Status, string? ContentType, string Body)
{
    public JsonElement Json => JsonDocument.Parse(Body).RootElement;
}
