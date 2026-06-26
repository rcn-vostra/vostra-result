using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vostra.Results;
using Vostra.Results.AspNetCore;

namespace Vostra.Results.Testing.Tests.RoundTrip;

/// <summary>Domain payload used by the round-trip endpoints.</summary>
public sealed record Product(int Id, string Name);

/// <summary>Spins up a real in-process server whose endpoints route Results through ToHttpResponse.</summary>
public sealed class TestApiHost : IAsyncLifetime
{
    private WebApplication? _app;

    public TestHttpClient Api { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddVostraResults();
        var app = builder.Build();

        app.MapGet("/products/{id}", (int id, HttpContext ctx) =>
        {
            Result<Product> result = id == 7
                ? new Product(7, "Widget")
                : new NotFoundError($"Product {id} not found", "Product.NotFound");
            return result.ToHttpResponse(ctx);
        });

        app.MapPost("/products", (Product input, HttpContext ctx) =>
        {
            Result<Product> result = string.IsNullOrWhiteSpace(input.Name)
                ? new ValidationError("Name is required.", field: "name", code: "Product.Validation")
                : Result.Created(new Product(99, input.Name));
            return result.ToHttpResponse(ctx);
        });

        app.MapGet("/products", (HttpContext ctx) =>
        {
            // The collection overload extends Result<IEnumerable<T>>, so type the result explicitly
            // (extension-method resolution does not apply the implicit T -> Result<T> conversion to the receiver).
            Result<IEnumerable<Product>> result = new[] { new Product(1, "a"), new Product(2, "b") };
            return result.ToHttpResponse(ctx, new Pagination(1, 20, 2));
        });

        app.MapDelete("/products/{id}", (int id, HttpContext ctx) =>
        {
            Result result = id == 7 ? Result.Success() : new NotFoundError("gone", "Product.NotFound");
            return result.ToHttpResponse(ctx);
        });

        await app.StartAsync();
        _app = app;
        Api = new TestHttpClient(app.GetTestClient(), "products");
    }

    public async Task DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }
}
