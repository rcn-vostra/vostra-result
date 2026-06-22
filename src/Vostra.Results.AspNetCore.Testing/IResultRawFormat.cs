using Vostra.Results;

namespace Vostra.Results.AspNetCore.Testing;

/// <summary>
/// Translates between HTTP request/response payloads and <see cref="Result{T}"/> building blocks.
/// This is the injection seam (serializer + envelope shape). The default is <see cref="RawJsonFormat"/>.
/// </summary>
public interface IResultRawFormat
{
    /// <summary>Serializes a request body to <see cref="HttpContent"/>.</summary>
    HttpContent SerializeRequest(object body);

    /// <summary>Deserializes the success envelope's <c>data</c> payload into <typeparamref name="T"/>.</summary>
    Task<T?> ReadData<T>(HttpResponseMessage response, CancellationToken ct);

    /// <summary>Deserializes a list success envelope into items plus pagination.</summary>
    Task<PagedList<T>?> ReadList<T>(HttpResponseMessage response, CancellationToken ct);

    /// <summary>Reconstructs the typed error(s) from an error response.</summary>
    Task<IReadOnlyList<ErrorBase>> ReadErrors(HttpResponseMessage response, CancellationToken ct);
}
