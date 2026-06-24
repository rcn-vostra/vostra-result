# NuGet Publish Prep — Report

**Branch:** `feat/nuget-publish-prep`  
**Date:** 2026-06-24  
**Scope:** §3 packaging fixes, §7 FluentAssertions downgrade, GitHub Actions workflows

---

## Files Changed

| File | Change |
|---|---|
| `Directory.Build.props` | Added: `Authors`/`Company` = `vostra.ai`, `Copyright`, `RepositoryUrl`, `PackageProjectUrl`, `Version=1.0.0-preview.1`, `PublishRepositoryUrl`, `EmbedUntrackedSources`, `IncludeSymbols`, `SymbolPackageFormat=snupkg`, THIRD-PARTY-NOTICES.md pack ItemGroup |
| `src/Vostra.Results/Vostra.Results.csproj` | Added: `PackageTags`, `IsTrimmable=true`, `IsAotCompatible=true`, `Microsoft.SourceLink.GitHub 8.0.0` |
| `src/Vostra.Results.AspNetCore/Vostra.Results.AspNetCore.csproj` | Added: `PackageTags`, `Microsoft.SourceLink.GitHub 8.0.0` |
| `src/Vostra.Results.Testing/Vostra.Results.Testing.csproj` | Added: `PackageTags`, `Microsoft.SourceLink.GitHub 8.0.0` |
| `src/Vostra.Results.AspNetCore.Testing/Vostra.Results.AspNetCore.Testing.csproj` | Added: `PackageTags`, `Microsoft.SourceLink.GitHub 8.0.0` |
| `THIRD-PARTY-NOTICES.md` | Expanded: full MIT license text for ErrorOr (Amichai Mantinband), FluentResults (Michael Altmann), OneOf (Harry McIntyre); FA test-only disclaimer |
| `tests/Vostra.Results.Tests/Vostra.Results.Tests.csproj` | FA downgrade: `8.10.0` → `7.2.0` |
| `tests/Vostra.Results.AspNetCore.Tests/Vostra.Results.AspNetCore.Tests.csproj` | FA downgrade: `8.10.0` → `7.2.0` |
| `tests/Vostra.Results.Testing.Tests/Vostra.Results.Testing.Tests.csproj` | FA downgrade: `8.10.0` → `7.2.0` |
| `tests/Vostra.Results.AspNetCore.Testing.Tests/Vostra.Results.AspNetCore.Testing.Tests.csproj` | FA downgrade: `8.10.0` → `7.2.0` |
| `.github/workflows/ci.yml` | Created: push+PR to main; checkout fetch-depth 0; setup-dotnet 8.0.x + 10.0.x; restore, build -c Release, test -c Release |
| `.github/workflows/release.yml` | Created: tag `v*` trigger; restore, build, test, pack with ContinuousIntegrationBuild=true; push in dependency order (Core → AspNetCore + Testing → AspNetCore.Testing) using tag-extracted version for exact filenames |

---

## FluentAssertions 7.x

**Version chosen:** `7.2.0` (Apache-2.0, last major Apache-licensed release line).

**Assertion API fixes required:** None. All existing test code compiled cleanly against FA 7.2.0 without modification. The `Should().Throw<T>().WithMessage(...)`, `ThrowAsync<T>()`, `BeOfType<T>()`, `ContainSingle()`, `HaveCount()`, `Equal()`, `Be()`, `Contain()`, `AllBeOfType<T>()`, `OnlyContain()`, `BeLessThanOrEqualTo()`, `BeEmpty()`, `BeTrue()` APIs are unchanged between FA 7.x and 8.x for the patterns used in this codebase.

---

## Verification — Build

```
dotnet build Vostra.Results.sln -c Release
```

**Result:** Build succeeded — 0 warnings, 0 errors (all 8 project/TFM combinations).

No AOT/trim warnings from `IsAotCompatible=true` + `IsTrimmable=true` on the Core project. The library is BCL-only and clean for AOT.

---

## Verification — Test

```
dotnet test Vostra.Results.sln -c Release
```

**Result:** All tests pass on both net8.0 and net10.0.

| Test Project | net8.0 | net10.0 |
|---|---|---|
| Vostra.Results.Tests | 109 passed | 109 passed |
| Vostra.Results.AspNetCore.Tests | 35 passed | 35 passed |
| Vostra.Results.Testing.Tests | 23 passed | 23 passed |
| Vostra.Results.AspNetCore.Testing.Tests | 36 passed | 36 passed |
| **Total** | **203** | **203** |

---

## Verification — Pack

```
dotnet pack Vostra.Results.sln -c Release -o ./artifacts
```

**Result:** 0 warnings (no NU5039, no NU5048). Produced:

```
artifacts/Vostra.Results.1.0.0-preview.1.nupkg
artifacts/Vostra.Results.1.0.0-preview.1.snupkg
artifacts/Vostra.Results.AspNetCore.1.0.0-preview.1.nupkg
artifacts/Vostra.Results.AspNetCore.1.0.0-preview.1.snupkg
artifacts/Vostra.Results.Testing.1.0.0-preview.1.nupkg
artifacts/Vostra.Results.Testing.1.0.0-preview.1.snupkg
artifacts/Vostra.Results.AspNetCore.Testing.1.0.0-preview.1.nupkg
artifacts/Vostra.Results.AspNetCore.Testing.1.0.0-preview.1.snupkg
```

4 `.nupkg` + 4 `.snupkg` as required.

---

## Nuspec Inspection — Vostra.Results.1.0.0-preview.1.nupkg

### Key fields (from `Vostra.Results.nuspec` inside the nupkg)

| Field | Value |
|---|---|
| `<id>` | `Vostra.Results` |
| `<version>` | `1.0.0-preview.1` |
| `<authors>` | `vostra.ai` |
| `<license type="expression">` | `MIT` |
| `<readme>` | `README.md` |
| `<projectUrl>` | `https://github.com/rcn-vostra/vostra-result` |
| `<copyright>` | `Copyright (c) vostra.ai` |
| `<tags>` | `result result-type error-handling errors-as-values railway-oriented functional` |
| `<repository>` | `type="git" url="https://github.com/rcn-vostra/vostra-result" branch="refs/heads/feat/nuget-publish-prep" commit="d68e619d..."` |
| Target frameworks | `net8.0`, `net10.0` |

All required fields present and correct.

### nupkg file list

```
_rels/.rels
Vostra.Results.nuspec
README.md
THIRD-PARTY-NOTICES.md          ← confirmed present
lib/net10.0/Vostra.Results.dll
lib/net10.0/Vostra.Results.xml
lib/net8.0/Vostra.Results.dll
lib/net8.0/Vostra.Results.xml
[Content_Types].xml
package/services/metadata/core-properties/...psmdcp
```

`THIRD-PARTY-NOTICES.md` is present at the root of the package.

---

## Deviations and Concerns

1. **Old 1.0.0 artifacts in `artifacts/`:** `artifacts/Vostra.Results.1.0.0.nupkg` and `artifacts/Vostra.Results.Testing.1.0.0.nupkg` exist from a prior session. The `artifacts/` directory is gitignored — these are harmless locally, but should be deleted before running a real publish to avoid confusion. The release workflow pushes by exact version-tagged filename so they won't be accidentally pushed.

2. **Release workflow push order:** Steps are strictly ordered (Core → AspNetCore → Testing → AspNetCore.Testing) and each step is a separate sequential GitHub Actions step, so NuGet will have Core available before dependent packages are pushed. The `--skip-duplicate` flag makes retries safe.

3. **`ContinuousIntegrationBuild` not set in `Directory.Build.props`:** As instructed, this is left out of props and is only passed via `-p:ContinuousIntegrationBuild=true` in the release workflow `dotnet pack` step. This means local dev packs don't set it (which is correct — deterministic-build mode on local machines causes problems with source paths).

4. **SourceLink.GitHub version 8.0.0:** Chosen per instructions. This is the stable release that works with net8.0 and net10.0 TFMs.

5. **FA 7.2.0 is Apache-2.0:** Confirmed. The last Apache-2.0 release line. No commercial license concern for CI.
