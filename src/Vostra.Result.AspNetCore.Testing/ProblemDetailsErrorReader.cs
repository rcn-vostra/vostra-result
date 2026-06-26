using System.Text.Json;
using Vostra.Result;

namespace Vostra.Result.AspNetCore.Testing;

/// <summary>
/// Reconstructs typed <see cref="ErrorBase"/> instances from an RFC 7807 <c>problem+json</c>
/// document produced by Vostra.Result.AspNetCore. Branches on the <c>errorType</c> extension:
/// validation responses carry a field→messages object map; multi-error responses carry an
/// <c>errors</c> array; single errors carry identity only in the top-level extensions.
/// </summary>
public static class ProblemDetailsErrorReader
{
    /// <summary>Reads the problem document into one or more typed errors.</summary>
    /// <param name="root">The parsed problem-details JSON object.</param>
    /// <param name="statusCode">The HTTP status code of the response.</param>
    public static IReadOnlyList<ErrorBase> Read(JsonElement root, int statusCode)
    {
        var errorTypeText = GetString(root, "errorType");
        var type = ParseType(errorTypeText);
        var code = GetString(root, "code") ?? "General.Unexpected";
        var detail = GetString(root, "detail") ?? GetString(root, "title") ?? $"HTTP {statusCode}";

        if (type == ErrorType.Validation
            && root.TryGetProperty("errors", out var map)
            && map.ValueKind == JsonValueKind.Object)
        {
            var validation = new List<ErrorBase>();
            foreach (var field in map.EnumerateObject())
            {
                if (field.Value.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var message in field.Value.EnumerateArray())
                {
                    var extra = new Dictionary<string, object?> { [ErrorBase.FieldMetadataKey] = field.Name };
                    validation.Add(Create(ErrorType.Validation, code, message.GetString() ?? string.Empty, statusCode, extra));
                }
            }

            if (validation.Count > 0)
            {
                return validation;
            }
        }

        if (root.TryGetProperty("errors", out var array) && array.ValueKind == JsonValueKind.Array)
        {
            var many = new List<ErrorBase>();
            foreach (var entry in array.EnumerateArray())
            {
                var entryCode = GetString(entry, "code") ?? code;
                var entryMessage = GetString(entry, "message") ?? detail;
                many.Add(Create(type, entryCode, entryMessage, statusCode));
            }

            if (many.Count > 0)
            {
                return many;
            }
        }

        return new[] { Create(type, code, detail, statusCode) };
    }

    private static ErrorBase Create(
        ErrorType type,
        string code,
        string message,
        int statusCode,
        IReadOnlyDictionary<string, object?>? extra = null)
    {
        var metadata = new Dictionary<string, object?> { ["status"] = statusCode };
        if (extra is not null)
        {
            foreach (var pair in extra)
            {
                metadata[pair.Key] = pair.Value;
            }
        }

        return type switch
        {
            ErrorType.Validation => new ValidationError(message, code: code, metadata: metadata),
            ErrorType.NotFound => new NotFoundError(message, code, metadata: metadata),
            ErrorType.Conflict => new ConflictError(message, code, metadata: metadata),
            ErrorType.Unauthorized => new UnauthorizedError(message, code, metadata: metadata),
            ErrorType.Forbidden => new ForbiddenError(message, code, metadata: metadata),
            _ => new Error(message, code, metadata: metadata),
        };
    }

    private static ErrorType ParseType(string? text) =>
        Enum.TryParse<ErrorType>(text, ignoreCase: true, out var parsed) ? parsed : ErrorType.Unexpected;

    private static string? GetString(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(property, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
