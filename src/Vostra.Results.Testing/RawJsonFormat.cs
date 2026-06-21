using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Vostra.Results;
using Vostra.Results.AspNetCore;

namespace Vostra.Results.Testing;

/// <summary>
/// Default <see cref="IResultRawFormat"/> backed by System.Text.Json. Reads the Vostra.Results.AspNetCore
/// success envelopes and reconstructs typed errors from <c>problem+json</c>. Payload deserialization is
/// strict (unmapped members throw a helpful hint); the envelope itself is parsed tolerantly.
/// </summary>
public sealed class RawJsonFormat : IResultRawFormat
{
    private const string MappingHint =
        " — MAKE SURE YOU HAVE MAPPED TO THE CORRECT RESPONSE OBJECT (THAT CONTROLLER RESPONDS WITH)";

    private static readonly JsonSerializerOptions Tolerant = new(JsonSerializerDefaults.Web);

    private static readonly JsonSerializerOptions Strict = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    /// <summary>A shared, stateless default instance.</summary>
    public static RawJsonFormat Instance { get; } = new();

    /// <inheritdoc />
    public HttpContent SerializeRequest(object body) =>
        new StringContent(JsonSerializer.Serialize(body, Tolerant), Encoding.UTF8, "application/json");

    /// <inheritdoc />
    public async Task<T?> ReadData<T>(HttpResponseMessage response, CancellationToken ct)
    {
        var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        using var document = JsonDocument.Parse(json);
        return document.RootElement.TryGetProperty("data", out var data)
            ? Deserialize<T>(data)
            : default;
    }

    /// <inheritdoc />
    public async Task<PagedList<T>?> ReadList<T>(HttpResponseMessage response, CancellationToken ct)
    {
        var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var items = new List<T>();
        if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in data.EnumerateArray())
            {
                var item = Deserialize<T>(element);
                if (item is not null)
                {
                    items.Add(item);
                }
            }
        }

        var pagination = root.TryGetProperty("pagination", out var p)
            ? p.Deserialize<Pagination>(Tolerant) ?? new Pagination(1, 0, 0)
            : new Pagination(1, 0, 0);

        return new PagedList<T>(items, pagination);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ErrorBase>> ReadErrors(HttpResponseMessage response, CancellationToken ct)
    {
        var status = (int)response.StatusCode;
        var body = (await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false)).Trim();

        if (body.Length == 0)
        {
            return new ErrorBase[] { new Error($"HTTP {status} {response.ReasonPhrase}".Trim(), "Http.Error") };
        }

        if (body[0] != '{' && body[0] != '[')
        {
            return new ErrorBase[] { new Error(body, "Http.Error") };
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            return ProblemDetailsErrorReader.Read(document.RootElement, status);
        }
        catch (JsonException)
        {
            return new ErrorBase[] { new Error(body, "Http.Error") };
        }
    }

    private static T? Deserialize<T>(JsonElement element)
    {
        try
        {
            return element.Deserialize<T>(Strict);
        }
        catch (JsonException ex) when (ex.Message.Contains("could not be mapped", StringComparison.OrdinalIgnoreCase))
        {
            throw new JsonException(ex.Message + MappingHint, ex);
        }
    }
}
