using System.Net;
using System.Net.Http;
using Vostra.Result;
using Vostra.Result.Testing;

namespace Vostra.Result.AspNetCore.Testing;

/// <summary>
/// An async test client that issues HTTP verbs against an API built with Vostra.Result.AspNetCore and
/// collapses each response into a <see cref="Result{T}"/>, reconstructing typed errors on failure.
/// </summary>
public sealed class TestHttpClient
{
    private readonly HttpClient _client;
    private readonly string _baseUrl;
    private readonly IResultRawFormat _format;

    /// <summary>Creates a client over an existing <see cref="HttpClient"/>.</summary>
    /// <param name="client">The underlying client (e.g. from a test server).</param>
    /// <param name="baseUrl">A base path prepended to every request URL.</param>
    /// <param name="format">The wire format; defaults to <see cref="RawJsonFormat.Instance"/>.</param>
    public TestHttpClient(HttpClient client, string baseUrl = "", IResultRawFormat? format = null)
    {
        _client = client;
        _baseUrl = baseUrl;
        _format = format ?? RawJsonFormat.Instance;
    }

    /// <summary>Issues GET and returns the deserialized value.</summary>
    public Task<Result<T>> Get<T>(string url = "", CancellationToken ct = default) =>
        SendData<T>(HttpMethod.Get, url, body: null, ct);

    /// <summary>Issues GET and returns a page of items plus pagination.</summary>
    public async Task<Result<PagedList<T>>> GetList<T>(string url = "", CancellationToken ct = default)
    {
        using var request = BuildRequest(HttpMethod.Get, url, body: null);
        using var response = await _client.SendAsync(request, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return Fail<PagedList<T>>(await _format.ReadErrors(response, ct).ConfigureAwait(false), HttpMethod.Get, url, body: null);
        }

        var page = await _format.ReadList<T>(response, ct).ConfigureAwait(false);
        return page is null
            ? AttachRequest(NullPayload($"GET {Combine(url)} returned no list payload"), HttpMethod.Get, url, body: null)
            : page;
    }

    /// <summary>Issues POST and returns the deserialized value.</summary>
    public Task<Result<T>> Post<T>(string url, object body, CancellationToken ct = default) =>
        SendData<T>(HttpMethod.Post, url, body, ct);

    /// <summary>Issues POST and returns a valueless result.</summary>
    public Task<Result> Post(string url, object body, CancellationToken ct = default) =>
        SendNoData(HttpMethod.Post, url, body, ct);

    /// <summary>Issues PUT and returns the deserialized value.</summary>
    public Task<Result<T>> Put<T>(string url, object body, CancellationToken ct = default) =>
        SendData<T>(HttpMethod.Put, url, body, ct);

    /// <summary>Issues PUT and returns a valueless result.</summary>
    public Task<Result> Put(string url, object body, CancellationToken ct = default) =>
        SendNoData(HttpMethod.Put, url, body, ct);

    /// <summary>Issues PATCH and returns the deserialized value.</summary>
    public Task<Result<T>> Patch<T>(string url, object body, CancellationToken ct = default) =>
        SendData<T>(HttpMethod.Patch, url, body, ct);

    /// <summary>Issues PATCH and returns a valueless result.</summary>
    public Task<Result> Patch(string url, object body, CancellationToken ct = default) =>
        SendNoData(HttpMethod.Patch, url, body, ct);

    /// <summary>Issues DELETE and returns a valueless result.</summary>
    public Task<Result> Delete(string url, CancellationToken ct = default) =>
        SendNoData(HttpMethod.Delete, url, body: null, ct);

    /// <summary>Issues DELETE and returns the deserialized value.</summary>
    public Task<Result<T>> Delete<T>(string url, CancellationToken ct = default) =>
        SendData<T>(HttpMethod.Delete, url, body: null, ct);

    private async Task<Result<T>> SendData<T>(HttpMethod method, string url, object? body, CancellationToken ct)
    {
        using var request = BuildRequest(method, url, body);
        using var response = await _client.SendAsync(request, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return Fail<T>(await _format.ReadErrors(response, ct).ConfigureAwait(false), method, url, body);
        }

        var data = await _format.ReadData<T>(response, ct).ConfigureAwait(false);
        if (data is null)
        {
            return AttachRequest(NullPayload($"{method.Method} {Combine(url)} returned null data"), method, url, body);
        }

        return response.StatusCode == HttpStatusCode.Created ? Result.Created(data) : data;
    }

    private async Task<Result> SendNoData(HttpMethod method, string url, object? body, CancellationToken ct)
    {
        using var request = BuildRequest(method, url, body);
        using var response = await _client.SendAsync(request, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var errors = await _format.ReadErrors(response, ct).ConfigureAwait(false);
            return AttachRequest(errors, method, url, body);
        }

        return response.StatusCode == HttpStatusCode.Created ? Result.Created() : Result.Ok();
    }

    private Result<T> Fail<T>(IReadOnlyList<ErrorBase> errors, HttpMethod method, string url, object? body) =>
        AttachRequest(errors, method, url, body);

    private static ErrorBase[] NullPayload(string message) =>
        new ErrorBase[] { new Error(message, "Http.NullResponse") };

    private ErrorBase[] AttachRequest(IReadOnlyList<ErrorBase> errors, HttpMethod method, string url, object? body)
    {
        var array = errors.Count == 0
            ? new ErrorBase[] { new Error($"{method.Method} {Combine(url)} failed", "Http.Error") }
            : errors.ToArray();

        array[0] = array[0].WithRequestContext(new RequestContext(method.Method, Combine(url), body));
        return array;
    }

    private HttpRequestMessage BuildRequest(HttpMethod method, string url, object? body)
    {
        var request = new HttpRequestMessage(method, Combine(url));
        if (body is not null)
        {
            request.Content = _format.SerializeRequest(body);
        }

        return request;
    }

    private string Combine(string url)
    {
        if (string.IsNullOrEmpty(_baseUrl))
        {
            return url.TrimStart('/');
        }

        var left = _baseUrl.TrimEnd('/');
        var right = url.TrimStart('/');
        return right.Length == 0 ? left : $"{left}/{right}";
    }
}
