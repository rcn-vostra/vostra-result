using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Vostra.Results.AspNetCore;

/// <summary>Maps <see cref="Result"/>/<see cref="Result{T}"/> onto an HTTP <see cref="IResult"/>.</summary>
public static class ToHttpResponseExtensions
{
    /// <summary>Maps a <see cref="Result{T}"/> to a 200/201 success envelope or an RFC 7807 error.</summary>
    public static IResult ToHttpResponse<T>(this Result<T> result, HttpContext http)
    {
        var operationId = OperationId(http);
        if (result.TryGetValue(out var value))
        {
            var envelope = new SuccessEnvelope<T> { OperationId = operationId, Data = value };
            return HttpResults.Json(envelope, statusCode: SuccessStatus(result.SuccessKind));
        }

        return ProblemResultBuilder.Build(result.Errors, OptionsFrom(http), operationId);
    }

    /// <summary>Maps a <see cref="Result{T}"/> of a sequence to a paginated list envelope or an RFC 7807 error.</summary>
    public static IResult ToHttpResponse<T>(this Result<IEnumerable<T>> result, HttpContext http, Pagination pagination)
    {
        var operationId = OperationId(http);
        if (result.TryGetValue(out var value))
        {
            var envelope = new ListEnvelope<T>
            {
                OperationId = operationId,
                Data = value.ToList(),
                Pagination = pagination,
            };
            return HttpResults.Json(envelope, statusCode: SuccessStatus(result.SuccessKind));
        }

        return ProblemResultBuilder.Build(result.Errors, OptionsFrom(http), operationId);
    }

    /// <summary>Maps a non-generic <see cref="Result"/> to a 200/201 (operation id only) or an RFC 7807 error.</summary>
    public static IResult ToHttpResponse(this Result result, HttpContext http)
    {
        var operationId = OperationId(http);
        if (result.IsSuccess)
        {
            var envelope = new SuccessEnvelope<object?> { OperationId = operationId, Data = null };
            return HttpResults.Json(envelope, statusCode: SuccessStatus(result.SuccessKind));
        }

        return ProblemResultBuilder.Build(result.Errors, OptionsFrom(http), operationId);
    }

    private static int SuccessStatus(SuccessKind kind) => kind == SuccessKind.Created ? 201 : 200;

    private static string OperationId(HttpContext http) => Activity.Current?.Id ?? http.TraceIdentifier;

    private static VostraResultsOptions OptionsFrom(HttpContext http) =>
        http.RequestServices?.GetService<VostraResultsOptions>() ?? VostraResultsOptions.Default;
}
