using System.Net;
using System.Net.Http;
using System.Text;

namespace Vostra.Results.Testing.Tests;

/// <summary>Returns a canned response and records the last request for assertions.</summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _status;
    private readonly string _body;
    private readonly string _contentType;

    public StubHttpMessageHandler(HttpStatusCode status, string body, string contentType = "application/json")
    {
        _status = status;
        _body = body;
        _contentType = contentType;
    }

    public HttpRequestMessage? LastRequest { get; private set; }
    public string? LastRequestBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        LastRequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        return new HttpResponseMessage(_status)
        {
            Content = new StringContent(_body, Encoding.UTF8, _contentType),
        };
    }

    public static HttpClient Client(HttpStatusCode status, string body, string contentType = "application/json") =>
        new(new StubHttpMessageHandler(status, body, contentType)) { BaseAddress = new Uri("http://localhost/") };
}
