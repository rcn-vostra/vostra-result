# PARKED — Vostra.Result.AspNetCore design (brainstorm, not yet a spec)

**Status:** paused on 2026-06-20 to finish Core first. Resume with the `brainstorming` skill, then
`writing-plans`. Branch `feat/vostra-results-aspnetcore` exists (off main) but holds no AspNetCore code yet.

## Decisions already settled in brainstorm (carry these in)
1. **Surface** = extension methods returning `Microsoft.AspNetCore.Http.IResult`:
   `result.ToHttpResponse(HttpContext)` for `Result<T>` and non-generic `Result`. Works in minimal APIs
   and controllers. (No `ControllerBase` inheritance — improvement over the old `ApiControllerBase`.)
2. **ErrorType → HTTP status mapping lives in this package** (Core stays HTTP-free), configured via DI:
   `services.AddVostraResults(o => { o.MapStatus(ErrorType.Conflict, 422); o.MapStatusForCode("Order.Locked", 423); })`.
   Built-in default map for the 7 ErrorTypes. Adding an Error subclass needs zero mapping edits (FR-10.2).
   **Precedence:** per-`Code` override beats per-`ErrorType` map (document it).
3. **Errors → RFC 7807 ProblemDetails**, error `code` in `Extensions["code"]`; validation errors →
   `ValidationProblemDetails` (field → messages). (OD-4 / FR-10.4 / FR-11.3.)
4. **Success envelope** = thin `{ operationId, data }`; `operationId` defaults to
   `Activity.Current?.Id ?? HttpContext.TraceIdentifier` (no CorrelationId dependency).
5. **201 vs 200** — RESOLVED via Core change B: `ToHttpResponse` reads Core's `SuccessKind` (Ok→200,
   Created→201). No separate `ToCreatedResponse` needed. (Was the open question; B adopted 2026-06-20.)

## Open items to resolve when resumed (don't lose these)
- **Pagination / collection overloads (FR-10.1)** — the brainstorm omitted them; the old `ApiControllerBase`
  had `ToHttpResponse<T,P>(Result<IEnumerable<T>>, P pagination)`. Required. Define `Pagination` + `ListEnvelope`.
- **Multi-error collapse rule** — validation → `ValidationProblemDetails` field map is decided, but a
  NON-validation multi-error `Result` (OD-2 allows N errors) needs a defined collapse: first-error-wins for
  status + an `errors[]` extension carrying all? Spec it.
- **FR-11.3 wire contract** — status + `code` → which `Error` subclass must be a SINGLE shared, documented
  table that AspNetCore (serialize) and Testing (deserialize) both consume, or typed-error assertions drift.
- Files (one responsibility each): `ToHttpResponseExtensions`, `VostraResultsOptions` + `AddVostraResults`,
  `ErrorProblemDetailsMapper`, success envelope types, default status map.
- Targets `net8.0;net9.0`, `FrameworkReference Microsoft.AspNetCore.App` only (NFR-1).
