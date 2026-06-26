using System.Text.Json.Serialization;

namespace Vostra.Result.AspNetCore;

/// <summary>Pagination metadata for a list response.</summary>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Items per page.</param>
/// <param name="TotalCount">Total items across all pages.</param>
public sealed record Pagination(
    [property: JsonPropertyName("page")] int Page,
    [property: JsonPropertyName("pageSize")] int PageSize,
    [property: JsonPropertyName("totalCount")] long TotalCount)
{
    /// <summary>Total number of pages; 0 when <see cref="PageSize"/> is not positive.</summary>
    [JsonPropertyName("totalPages")]
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

/// <summary>Thin success envelope carrying the operation id and the payload.</summary>
/// <typeparam name="T">The payload type.</typeparam>
public sealed class SuccessEnvelope<T>
{
    /// <summary>Correlation/operation id for the request.</summary>
    [JsonPropertyName("operationId")]
    public string? OperationId { get; init; }

    /// <summary>The response payload.</summary>
    [JsonPropertyName("data")]
    public T? Data { get; init; }
}

/// <summary>Success envelope for a valueless (non-generic) result — carries only the operation id.</summary>
internal sealed class SuccessNoDataEnvelope
{
    /// <summary>Correlation/operation id for the request.</summary>
    [JsonPropertyName("operationId")]
    public string? OperationId { get; init; }
}

/// <summary>Success envelope for a paginated list.</summary>
/// <typeparam name="T">The element type.</typeparam>
public sealed class ListEnvelope<T>
{
    /// <summary>Correlation/operation id for the request.</summary>
    [JsonPropertyName("operationId")]
    public string? OperationId { get; init; }

    /// <summary>The page of items.</summary>
    [JsonPropertyName("data")]
    public IReadOnlyList<T> Data { get; init; } = Array.Empty<T>();

    /// <summary>Pagination metadata.</summary>
    [JsonPropertyName("pagination")]
    public Pagination Pagination { get; init; } = new(1, 0, 0);
}
