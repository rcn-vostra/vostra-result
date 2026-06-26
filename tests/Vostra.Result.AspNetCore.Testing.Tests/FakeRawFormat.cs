using System.Net.Http;
using System.Text;
using Vostra.Result;
using Vostra.Result.Testing;

namespace Vostra.Result.Testing.Tests;

internal sealed class FakeRawFormat : IResultRawFormat
{
    private readonly IReadOnlyList<ErrorBase> _errors;

    public FakeRawFormat(IReadOnlyList<ErrorBase> errors)
    {
        _errors = errors;
    }

    public HttpContent SerializeRequest(object body) =>
        new StringContent("{}", Encoding.UTF8, "application/json");

    public Task<T?> ReadData<T>(HttpResponseMessage response, CancellationToken ct) =>
        Task.FromResult(default(T));

    public Task<PagedList<T>?> ReadList<T>(HttpResponseMessage response, CancellationToken ct) =>
        Task.FromResult(default(PagedList<T>));

    public Task<IReadOnlyList<ErrorBase>> ReadErrors(HttpResponseMessage response, CancellationToken ct) =>
        Task.FromResult(_errors);
}
