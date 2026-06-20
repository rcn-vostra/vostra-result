namespace Vostra.Results;

/// <summary>
/// Transport-neutral kind of a successful result. Symmetric with <see cref="ErrorType"/>;
/// carries no HTTP semantics (a boundary maps <c>Ok</c>→200, <c>Created</c>→201).
/// </summary>
public enum SuccessKind
{
    /// <summary>An ordinary success.</summary>
    Ok,

    /// <summary>A success that created a new resource.</summary>
    Created,
}
