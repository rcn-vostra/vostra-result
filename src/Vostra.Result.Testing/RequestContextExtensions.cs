using Vostra.Result;

namespace Vostra.Result.Testing;

/// <summary>Attaching diagnostic <see cref="RequestContext"/> to an error, for any transport.</summary>
public static class RequestContextExtensions
{
    /// <summary>Metadata key under which a <see cref="RequestContext"/> is attached to an error.</summary>
    public const string RequestMetadataKey = "request";

    /// <summary>Returns a copy of <paramref name="error"/> with <paramref name="context"/> attached for
    /// diagnostics, preserving any existing metadata and the concrete error type.</summary>
    public static ErrorBase WithRequestContext(this ErrorBase error, RequestContext context)
    {
        var metadata = error.Metadata is null
            ? new Dictionary<string, object?>()
            : new Dictionary<string, object?>(error.Metadata);
        metadata[RequestMetadataKey] = context;
        return error.WithMetadata(metadata);
    }
}
