# NuGet Publish + Pre-1.0 Hardening — Plan

**Created:** 2026-06-21 · **Work it:** 2026-06-22 (tomorrow) · **Branch state:** all merged to `main`, pushed.

Captures (a) the NuGet account/publish path, (b) the decisions still to make, and (c) the full-project
code-review findings (blocking packaging gaps + non-blocking improvements + test gaps). Nothing here is
done yet — this is the worklist.

---

## 0. TL;DR — what blocks a clean publish

The **code is in good shape** (0 build warnings; tests green: 101 Core + 35 AspNetCore + 58 Testing per
TFM on net8.0/net9.0). What's missing is **packaging metadata + publish infra + a couple of stated DoD
gates** (benchmark, source-link). Order of work tomorrow: account → decisions → packaging fixes → publish.

---

## 1. NuGet account setup (the rabbit hole — do first, manually)

> User is handling this tomorrow; it's a chain of signups. Rough path to expect:

1. **nuget.org sign-in** → routes to a **Microsoft account** (personal or work).
2. MS account auth (the "bla account, then blabla account" hops — pick ONE identity and stick with it).
3. **New-user / publisher registration** page on nuget.org.
4. **Registration submission** page (username, email confirm).
5. **Account/offer-detail** page (terms acceptance).
6. Create an **API key** (scoped: *Push new packages and package versions*; glob `Vostra.*`; pick expiry).
7. **Reserve the `Vostra.` ID prefix** (nuget.org → Account → *Reserve ID prefix*; requires the account to
   meet the prefix-reservation criteria — may need the first package published first, then request).

Decide the **owner identity**: personal account vs. a `Vostra` org account. If this is meant to look like
an org-owned OSS project, create/use an org. This affects `Authors`/`Company` (currently the placeholder
"Vostra") and the project URL.

---

## 2. Decisions to make (were mid-discussion when we paused)

| # | Decision | Options | Leaning |
|---|----------|---------|---------|
| D1 | Publish mechanism | GitHub Actions (tag-triggered) **vs** manual `dotnet pack`/`push` **vs** decide later | **GitHub Actions** — reproducible, source-linked, matches NFR-8 |
| D2 | Initial version | `1.0.0-preview.1` **vs** `1.0.0` stable **vs** `0.x.0` | **`1.0.0-preview.1`** — validate the public API (esp. the brand-new DU surface) before SemVer locks in |
| D3 | Prefix reservation | reserve `Vostra.` **vs** not | **Reserve** |
| D4 | Owner identity | personal **vs** `Vostra` org | open — drives Authors/URL |
| D5 | Version coupling | lockstep all 3 **vs** independent | **lockstep** for v1 (simpler; Core must publish first so the other two can restore) |

Note: the 3 packages have a dependency chain (`Results` ← `AspNetCore` ← `Testing`). The SDK rewrites
`ProjectReference` → versioned `PackageReference` at pack time, so **Core must be on nuget.org first**.

---

## 3. Blocking packaging fixes (must do before publish)

From `dotnet pack` nuspec inspection — these are concretely missing across `Directory.Build.props` and all
three `src/*.csproj`:

- [ ] **`Version`** — currently defaults to `1.0.0`; set explicitly (see D2). Centralize in `Directory.Build.props`.
- [ ] **SourceLink** — add `Microsoft.SourceLink.GitHub`, `PublishRepositoryUrl=true`,
      `EmbedUntrackedSources=true`, `ContinuousIntegrationBuild=true` (in CI). Verified missing: packed
      nuspec has `<repository … commit="…">` with **no `url`**, so Source Link can't resolve.
- [ ] **`RepositoryUrl`** + **`PackageProjectUrl`** = `https://github.com/rcn-vostra/vostra-result`.
- [ ] **Symbol packages** — `IncludeSymbols=true`, `SymbolPackageFormat=snupkg`.
- [ ] **`PackageTags`** — none set on any package.
- [ ] **`Authors`/`Company`** — replace the "Vostra" placeholder (tied to D4).
- [ ] **THIRD-PARTY-NOTICES.md** — (a) expand it: it currently has only copyright one-liners, **not** the
      full MIT permission text the borrowed ErrorOr/FluentResults/OneOf licenses require to be reproduced;
      (b) pack it into every `.nupkg`:
      `<None Include="..\..\THIRD-PARTY-NOTICES.md" Pack="true" PackagePath="\" />`. (Required by req §7.)
- [ ] **(Optional but on-brand)** AOT/trim markers on Core: `IsTrimmable`, `IsAotCompatible` (NFR-4 claims
      AOT-safety; mark it so consumers/analyzers see it).

Already OK: `PackageId`, `Description`, `PackageLicenseExpression=MIT`, `PackageReadmeFile` packed,
`GenerateDocumentationFile` (xml packed). Core references BCL only; AspNetCore uses
`FrameworkReference Microsoft.AspNetCore.App` (NFR-1 satisfied — no third-party runtime deps). Root
`LICENSE` exists but isn't packed — fine, since `PackageLicenseExpression` is used.

---

## 4. Stated DoD gates currently unmet (req §9)

- [ ] **§9.7 / NFR-2 — benchmark.** No `*Benchmark*` project exists. The "success path is zero-alloc" claim
      is asserted but never measured. Add a **BenchmarkDotNet** project proving zero allocations on a
      3-step happy-path `Then` chain. (Until then, scope the README claim to "single-result happy path";
      note `Combine`/`SelectAsync` allocate.)
- [ ] **§9.9 — source-linked packaging.** Met once SourceLink + symbols land (§3).

---

## 5. Code-review findings — non-blocking improvements (decide, then schedule)

Ranked; none block publish but several are **SemVer-relevant** (cheaper to fix before 1.0):

1. **Validation per-error `Code` round-trip loss (fidelity).** `ProblemResultBuilder.BuildValidation`
   (`ProblemResultBuilder.cs:45-63`) emits only `errors[0].Code` + a field→messages map;
   `ProblemDetailsErrorReader` (`ProblemDetailsErrorReader.cs:38-39`) then rebuilds every validation error
   with that one top-level code. Two distinct validation codes → collapse to one on round-trip. No test
   pins this. **Decide:** is per-error code fidelity in scope for v1?
2. **Concrete error subtype not recoverable from wire — only `ErrorType`.** `AlreadyExistsError`
   reconstructs as `ConflictError`; custom subclasses reconstruct as the built-in for their `ErrorType`
   (`ProblemDetailsErrorReader.Create:84-92`). Acceptable by design (assertions are code/type-based) —
   **just document it** (`is AlreadyExistsError` won't hold after a round-trip).
3. **`VostraResultsOptions` uses plain `Dictionary` (NFR-5).** Public mutators on a DI singleton
   (`VostraResultsOptions.cs:9-27`) read concurrently per request. Safe only if mutated at startup. **Fix
   or document:** make setters internal / seal after `AddVostraResults`, or doc "configure at startup only".
4. **`SelectAsync` leaks thrown exceptions** past `Task.WhenAll` instead of turning them into error values
   (`ResultAggregate.cs:44-71`). Has an internal comment only. **Fix:** wrap selector body in try/catch →
   `new Error(...).WithCausedBy(ex)`, and/or surface the warning in the **XML doc**. Also takes no
   `CancellationToken` — a hung selector can't be cancelled.
5. **`Combine` allocates** via LINQ `Where/SelectMany/ToArray` (`ResultAggregate.cs:8,15`). Fine for an
   aggregation API; just note NFR-2 is scoped to the single-result happy path.
6. **Testing null-payload conflation.** `TestHttpClient.SendData<T>` treats `data is null` as a missing
   payload (`TestHttpClient.cs:90-93`) → a legitimately-null reference payload misreports as
   `Http.NullResponse`. Edge case; guard on present-vs-null.
7. **Multi-success API asymmetry (SemVer-relevant).** `Result<T1,T2>` / `Result<T1,T2,T3>` are
   terminal-match-only by design, but also lack `TryGet`/`GetValueOr`/`MatchFirst` that `Result<T>` has.
   **Decide** whether that minimal surface is the intended v1 lock-in.
8. **README missing zero-alloc/AOT statements** despite NFR-2/NFR-4 being headline selling points.

---

## 6. Test-coverage gaps to close (cheap, do alongside §5)

- [ ] `default(Result<T1,T2>)` and `default(Result<T1,T2,T3>)` — uninitialized-struct discipline untested
      for the multi-success types (correct by inspection; no regression test).
- [ ] Cross-arm equality for `T1==T2` beyond the single `First(0)`/`Second(0)` case; `default == default`
      and `default`-vs-`Err(sentinel)` equality for all types.
- [ ] `SelectAsync` exception-propagation behavior (the documented sharp edge has no test).
- [ ] Validation per-error-code round-trip (finding §5.1) — add a failing test so CI sees the gap.
- [ ] `MapError` map-to-empty-array re-substitution (degenerate case).

---

## 7. Licensing flag (CI/legal — not a package issue)

- [ ] **FluentAssertions 8.10.0** in `tests/Vostra.Results.Tests` (`…Tests.csproj:14`) is **commercially
      licensed** (v8), per our own CLAUDE.md. Test-only, never shipped — but a public MIT repo running a
      paid-license lib in CI without a license is real exposure. **Decide:** pin to FluentAssertions ≤ 7.x
      (last Apache-2.0 line) or migrate the assertions. (Affects all three test projects if they share it.)

---

## 8. Suggested order tomorrow

1. Account + API key + (identity decision D4). → unblocks everything.
2. Settle D1/D2/D3/D5.
3. Packaging fixes (§3) + THIRD-PARTY-NOTICES expansion.
4. Decide FluentAssertions (§7) — quick, do early so CI is clean.
5. (If GitHub Actions) write the release workflow; else dry-run `dotnet pack` and inspect.
6. Benchmark project (§4) — can run in parallel; needed for the §9.7 gate but not for a *preview* push.
7. Non-blocking fixes (§5) + test gaps (§6) — fold the SemVer-relevant ones (5.1, 5.7) in **before** 1.0,
   the rest can trail.
8. Publish Core first, then AspNetCore, then Testing (dependency order).
