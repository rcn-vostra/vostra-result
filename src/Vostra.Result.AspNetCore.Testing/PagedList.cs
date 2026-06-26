using Vostra.Result.AspNetCore;

namespace Vostra.Result.AspNetCore.Testing;

/// <summary>A page of items plus its pagination metadata, returned by <c>TestHttpClient.GetList</c>.</summary>
/// <typeparam name="T">The element type.</typeparam>
/// <param name="Items">The items on this page.</param>
/// <param name="Pagination">The pagination metadata (reused from Vostra.Result.AspNetCore).</param>
public sealed record PagedList<T>(IReadOnlyList<T> Items, Pagination Pagination);
