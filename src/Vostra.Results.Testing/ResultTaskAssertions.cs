using Vostra.Results;

namespace Vostra.Results.Testing;

/// <summary>Async overloads of <see cref="ResultAssertions"/> for <see cref="Task{TResult}"/>-wrapped results,
/// so call sites need no stray <c>await</c>.</summary>
public static class ResultTaskAssertions
{
    /// <summary>Awaits then asserts success, returning the value (terminal).</summary>
    public static async Task<T> ShouldBeSuccess<T>(this Task<Result<T>> task) =>
        (await task.ConfigureAwait(false)).ShouldBeSuccess();

    /// <summary>Awaits then asserts success (valueless), returning the result.</summary>
    public static async Task<Result> ShouldBeSuccess(this Task<Result> task) =>
        (await task.ConfigureAwait(false)).ShouldBeSuccess();

    /// <summary>Awaits then asserts an error with <paramref name="code"/>.</summary>
    public static async Task<Result<T>> ShouldHaveError<T>(this Task<Result<T>> task, string code) =>
        (await task.ConfigureAwait(false)).ShouldHaveError(code);

    /// <summary>Awaits then asserts an error of <paramref name="type"/>.</summary>
    public static async Task<Result<T>> ShouldHaveError<T>(this Task<Result<T>> task, ErrorType type) =>
        (await task.ConfigureAwait(false)).ShouldHaveError(type);

    /// <summary>Awaits then asserts an error with <paramref name="code"/>.</summary>
    public static async Task<Result> ShouldHaveError(this Task<Result> task, string code) =>
        (await task.ConfigureAwait(false)).ShouldHaveError(code);

    /// <summary>Awaits then asserts an error of <paramref name="type"/>.</summary>
    public static async Task<Result> ShouldHaveError(this Task<Result> task, ErrorType type) =>
        (await task.ConfigureAwait(false)).ShouldHaveError(type);

    /// <summary>Awaits then asserts success and returns the result for chaining (no inspection).</summary>
    public static async Task<Result<T>> Assert<T>(this Task<Result<T>> task) =>
        (await task.ConfigureAwait(false)).Assert();

    /// <summary>Awaits then asserts success (valueless) and returns the result for chaining.</summary>
    public static async Task<Result> Assert(this Task<Result> task) =>
        (await task.ConfigureAwait(false)).Assert();

    /// <summary>Awaits then asserts success and runs <paramref name="assertion"/> on the value.</summary>
    public static async Task<Result<T>> Assert<T>(this Task<Result<T>> task, Action<T> assertion) =>
        (await task.ConfigureAwait(false)).Assert(assertion);

    /// <summary>Awaits then asserts a NotFound error.</summary>
    public static async Task<Result<T>> ShouldBeNotFound<T>(this Task<Result<T>> task) =>
        (await task.ConfigureAwait(false)).ShouldBeNotFound();

    /// <summary>Awaits then asserts a NotFound error.</summary>
    public static async Task<Result> ShouldBeNotFound(this Task<Result> task) =>
        (await task.ConfigureAwait(false)).ShouldBeNotFound();

    /// <summary>Awaits then asserts a Validation error.</summary>
    public static async Task<Result<T>> ShouldBeValidation<T>(this Task<Result<T>> task) =>
        (await task.ConfigureAwait(false)).ShouldBeValidation();

    /// <summary>Awaits then asserts a Validation error.</summary>
    public static async Task<Result> ShouldBeValidation(this Task<Result> task) =>
        (await task.ConfigureAwait(false)).ShouldBeValidation();

    /// <summary>Awaits then asserts a Conflict error.</summary>
    public static async Task<Result<T>> ShouldBeConflict<T>(this Task<Result<T>> task) =>
        (await task.ConfigureAwait(false)).ShouldBeConflict();

    /// <summary>Awaits then asserts a Conflict error.</summary>
    public static async Task<Result> ShouldBeConflict(this Task<Result> task) =>
        (await task.ConfigureAwait(false)).ShouldBeConflict();

    /// <summary>Awaits then asserts an Unauthorized error.</summary>
    public static async Task<Result<T>> ShouldBeUnauthorized<T>(this Task<Result<T>> task) =>
        (await task.ConfigureAwait(false)).ShouldBeUnauthorized();

    /// <summary>Awaits then asserts an Unauthorized error.</summary>
    public static async Task<Result> ShouldBeUnauthorized(this Task<Result> task) =>
        (await task.ConfigureAwait(false)).ShouldBeUnauthorized();

    /// <summary>Awaits then asserts a Forbidden error.</summary>
    public static async Task<Result<T>> ShouldBeForbidden<T>(this Task<Result<T>> task) =>
        (await task.ConfigureAwait(false)).ShouldBeForbidden();

    /// <summary>Awaits then asserts a Forbidden error.</summary>
    public static async Task<Result> ShouldBeForbidden(this Task<Result> task) =>
        (await task.ConfigureAwait(false)).ShouldBeForbidden();

    /// <summary>Awaits then asserts an Unexpected error.</summary>
    public static async Task<Result<T>> ShouldBeUnexpected<T>(this Task<Result<T>> task) =>
        (await task.ConfigureAwait(false)).ShouldBeUnexpected();

    /// <summary>Awaits then asserts an Unexpected error.</summary>
    public static async Task<Result> ShouldBeUnexpected(this Task<Result> task) =>
        (await task.ConfigureAwait(false)).ShouldBeUnexpected();
}
