using Vostra.Results.AspNetCore;

namespace Vostra.Results.Testing;

/// <summary>A page of items plus its pagination metadata, returned by <c>TestHttpClient.GetList</c>.</summary>
/// <typeparam name="T">The element type.</typeparam>
/// <param name="Items">The items on this page.</param>
/// <param name="Pagination">The pagination metadata (reused from Vostra.Results.AspNetCore).</param>
public sealed record PagedList<T>(IReadOnlyList<T> Items, Pagination Pagination);
