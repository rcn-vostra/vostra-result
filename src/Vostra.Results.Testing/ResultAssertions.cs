using System.Text;
using Vostra.Results;

namespace Vostra.Results.Testing;

/// <summary>Fluent, zero-dependency assertions over <see cref="Result{T}"/> and <see cref="Result"/>.</summary>
public static class ResultAssertions
{
    /// <summary>Asserts success and returns the value (terminal).</summary>
    public static T ShouldBeSuccess<T>(this Result<T> result)
    {
        if (result.TryGetValue(out var value))
        {
            return value;
        }

        throw Fail("Expected a successful result, but it failed.", result.Errors);
    }

    /// <summary>Asserts success (valueless) and returns the result for chaining.</summary>
    public static Result ShouldBeSuccess(this Result result)
    {
        if (result.IsError)
        {
            throw Fail("Expected a successful result, but it failed.", result.Errors);
        }

        return result;
    }

    /// <summary>Asserts that some error carries <paramref name="code"/>; returns the result for chaining.</summary>
    public static Result<T> ShouldHaveError<T>(this Result<T> result, string code)
    {
        AssertHasError(result.IsError, result.Errors, e => e.Code == code, $"error with code '{code}'");
        return result;
    }

    /// <summary>Asserts that some error carries <paramref name="code"/>; returns the result for chaining.</summary>
    public static Result ShouldHaveError(this Result result, string code)
    {
        AssertHasError(result.IsError, result.Errors, e => e.Code == code, $"error with code '{code}'");
        return result;
    }

    /// <summary>Asserts that some error is of <paramref name="type"/>; returns the result for chaining.</summary>
    public static Result<T> ShouldHaveError<T>(this Result<T> result, ErrorType type)
    {
        AssertHasError(result.IsError, result.Errors, e => e.Type == type, $"error of type '{type}'");
        return result;
    }

    /// <summary>Asserts that some error is of <paramref name="type"/>; returns the result for chaining.</summary>
    public static Result ShouldHaveError(this Result result, ErrorType type)
    {
        AssertHasError(result.IsError, result.Errors, e => e.Type == type, $"error of type '{type}'");
        return result;
    }

    /// <summary>Asserts success and returns the result for chaining (no inspection).</summary>
    public static Result<T> Assert<T>(this Result<T> result)
    {
        result.ShouldBeSuccess();
        return result;
    }

    /// <summary>Asserts success (valueless) and returns the result for chaining.</summary>
    public static Result Assert(this Result result) => result.ShouldBeSuccess();

    /// <summary>Asserts success then runs <paramref name="assertion"/> on the value; returns the result.</summary>
    public static Result<T> Assert<T>(this Result<T> result, Action<T> assertion)
    {
        var value = result.ShouldBeSuccess();
        assertion(value);
        return result;
    }

    /// <summary>Asserts a NotFound error.</summary>
    public static Result<T> ShouldBeNotFound<T>(this Result<T> result) => result.ShouldHaveError(ErrorType.NotFound);
    /// <summary>Asserts a NotFound error.</summary>
    public static Result ShouldBeNotFound(this Result result) => result.ShouldHaveError(ErrorType.NotFound);
    /// <summary>Asserts a Validation error.</summary>
    public static Result<T> ShouldBeValidation<T>(this Result<T> result) => result.ShouldHaveError(ErrorType.Validation);
    /// <summary>Asserts a Validation error.</summary>
    public static Result ShouldBeValidation(this Result result) => result.ShouldHaveError(ErrorType.Validation);
    /// <summary>Asserts a Conflict error.</summary>
    public static Result<T> ShouldBeConflict<T>(this Result<T> result) => result.ShouldHaveError(ErrorType.Conflict);
    /// <summary>Asserts a Conflict error.</summary>
    public static Result ShouldBeConflict(this Result result) => result.ShouldHaveError(ErrorType.Conflict);
    /// <summary>Asserts an Unauthorized error.</summary>
    public static Result<T> ShouldBeUnauthorized<T>(this Result<T> result) => result.ShouldHaveError(ErrorType.Unauthorized);
    /// <summary>Asserts an Unauthorized error.</summary>
    public static Result ShouldBeUnauthorized(this Result result) => result.ShouldHaveError(ErrorType.Unauthorized);
    /// <summary>Asserts a Forbidden error.</summary>
    public static Result<T> ShouldBeForbidden<T>(this Result<T> result) => result.ShouldHaveError(ErrorType.Forbidden);
    /// <summary>Asserts a Forbidden error.</summary>
    public static Result ShouldBeForbidden(this Result result) => result.ShouldHaveError(ErrorType.Forbidden);
    /// <summary>Asserts an Unexpected error.</summary>
    public static Result<T> ShouldBeUnexpected<T>(this Result<T> result) => result.ShouldHaveError(ErrorType.Unexpected);
    /// <summary>Asserts an Unexpected error.</summary>
    public static Result ShouldBeUnexpected(this Result result) => result.ShouldHaveError(ErrorType.Unexpected);

    private static void AssertHasError(bool isError, IReadOnlyList<ErrorBase> errors, Func<ErrorBase, bool> predicate, string expectation)
    {
        if (!isError)
        {
            throw FailHeadlineOnly($"Expected an {expectation}, but the result was successful.");
        }

        if (!errors.Any(predicate))
        {
            throw Fail($"Expected an {expectation}, but none matched.", errors);
        }
    }

    private static VostraAssertionException Fail(string headline, IReadOnlyList<ErrorBase> errors) =>
        new(headline + Environment.NewLine + Describe(errors));

    private static VostraAssertionException FailHeadlineOnly(string headline) =>
        new(headline);

    /// <summary>Composes the rich diagnostic: each error's identity plus any attached request context.</summary>
    internal static string Describe(IReadOnlyList<ErrorBase> errors)
    {
        if (errors.Count == 0)
        {
            return "  (no errors)";
        }

        var builder = new StringBuilder();
        foreach (var error in errors)
        {
            builder.Append("  - ").Append(error.Type).Append(' ').Append(error.Code).Append(": ").Append(error.Message);
            if (error.Metadata is { } metadata
                && metadata.TryGetValue(RequestContextExtensions.RequestMetadataKey, out var raw)
                && raw is RequestContext request)
            {
                builder.Append(Environment.NewLine).Append("    request: ")
                    .Append(request.Operation).Append(' ').Append(request.Target);
                if (request.Body is not null)
                {
                    builder.Append(" | body: ").Append(request.Body);
                }
            }

            builder.Append(Environment.NewLine);
        }

        return builder.ToString().TrimEnd();
    }
}
