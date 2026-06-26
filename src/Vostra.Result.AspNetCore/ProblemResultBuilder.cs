using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Vostra.Result.AspNetCore;

/// <summary>Builds the RFC 7807 error <see cref="IResult"/> from one or more errors.</summary>
internal static class ProblemResultBuilder
{
    private const string ProblemJson = "application/problem+json";

    /// <summary>Builds an error response. All-validation errors render a field map; otherwise ProblemDetails.</summary>
    internal static IResult Build(IReadOnlyList<ErrorBase> errors, VostraResultsOptions options, string operationId)
    {
        var first = errors[0];
        var status = ErrorStatusResolver.Resolve(first, options);

        if (AllValidation(errors))
        {
            return BuildValidation(errors, status, operationId);
        }

        var problem = new ProblemDetails
        {
            Type = "about:blank",
            Title = ReasonPhrase(status),
            Status = status,
            Detail = first.Message,
        };
        problem.Extensions["code"] = first.Code;
        problem.Extensions["errorType"] = first.Type.ToString();
        problem.Extensions["operationId"] = operationId;

        if (errors.Count > 1)
        {
            problem.Extensions["errors"] = errors
                .Select(e => new ErrorEntry(e.Code, e.Message))
                .ToArray();
        }

        return HttpResults.Json(problem, statusCode: status, contentType: ProblemJson);
    }

    private static IResult BuildValidation(IReadOnlyList<ErrorBase> errors, int status, string operationId)
    {
        var map = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var group in errors.GroupBy(FieldKey))
        {
            map[group.Key] = group.Select(e => e.Message).ToArray();
        }

        var problem = new HttpValidationProblemDetails(map)
        {
            Type = "about:blank",
            Title = ReasonPhrase(status),
            Status = status,
        };
        problem.Extensions["code"] = errors[0].Code;
        problem.Extensions["errorType"] = ErrorType.Validation.ToString();
        problem.Extensions["operationId"] = operationId;

        return HttpResults.Json(problem, statusCode: status, contentType: ProblemJson);
    }

    private static bool AllValidation(IReadOnlyList<ErrorBase> errors)
    {
        foreach (var e in errors)
        {
            if (e.Type != ErrorType.Validation)
            {
                return false;
            }
        }

        return true;
    }

    private static string FieldKey(ErrorBase error)
    {
        if (error.Metadata is { } meta && meta.TryGetValue(ErrorBase.FieldMetadataKey, out var value) && value is string field)
        {
            return field;
        }

        return error.Code;
    }

    private static string ReasonPhrase(int status) => status switch
    {
        400 => "Bad Request",
        401 => "Unauthorized",
        403 => "Forbidden",
        404 => "Not Found",
        409 => "Conflict",
        422 => "Unprocessable Entity",
        500 => "Internal Server Error",
        _ => "Error",
    };

    private sealed record ErrorEntry(
        [property: JsonPropertyName("code")] string Code,
        [property: JsonPropertyName("message")] string Message);
}
