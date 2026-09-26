# AGENTS.md

## What this repo is

- This repository is a **versioning sample**, not a feature-rich application. The app is now a minimal ASP.NET Core web app with a root page and a `/version` endpoint.
- The important behavior lives in `version.json`, `dotnet-tools.json`, `nerdbank-versioning.sh`, `Dockerfile`, and `.github/workflows/build-and-publish.yml`.

## Big picture

- **Local versioning path:** `version.json` + the local `nbgv` tool from `dotnet-tools.json` define commit-height versions such as `1.0.0-preview.5`, with `v{version}` release tags.
- **CI versioning path:** `.github/workflows/build-and-publish.yml` uses the same NBGV version engine; preview merges publish the committed preview version, while RC/stable tags publish the matching release.
- For the GitHub Flow model used here (`main` + short-lived feature/fix branches), the practical strategy is **planned next version + automatic commit height**: ordinary merges advance `preview.{height}`, an RC tag promotes to staging, and a clean `vX.Y.Z` tag promotes to production.

## Files that matter most

- `version.json` (repo root): canonical local version rules. Read this first for tag patterns, preview/release behavior, and cloud-build variables.
- `.github/workflows/build-and-publish.yml`: release pipeline example; also shows how deployment environment is inferred from refs/tags.
- `Dockerfile`: accepts CI-calculated version build args, passes them to `dotnet publish`, and exposes the calculated runtime version metadata through container environment variables.
- `dotnet-tools.json` (repo root) + `nerdbank-versioning.sh`: local tool restore plus a disposable Git history simulator for preview/RC/prod scenarios.
- `Program.cs` + `VersionInfoProvider.cs`: minimal web host plus the `/version` endpoint; runtime version display prefers a sibling `version.json` file and falls back to assembly metadata stamped by `Nerdbank.GitVersioning`.
- `tests/versioning-samples.Tests`: smoke test coverage for the `/version` endpoint.
- `versioning-samples.csproj`: minimal ASP.NET Core project targeting `net10.0`; the only package reference is `Nerdbank.GitVersioning` for assembly/version stamping.

## Verified local workflow

```bash
dotnet tool restore

dotnet nbgv get-version

dotnet build ./src/versioning-samples.csproj
```

- Root-level `dotnet nbgv get-version` works when the checkout is inside a Git repository.
- `dotnet build ./src/versioning-samples.csproj` succeeds locally with SDK `10.0.108`.
- `dotnet nbgv get-version ...` currently fails in this workspace because the checkout is **not inside a Git repository**.

## Repo-specific conventions

- Version tags in `version.json` are expected in the form `vX.Y.Z`; `main` is the public-release branch.
- Preview builds are modeled as `*-preview.{height}` versions locally; production releases are clean `vX.Y.Z` tags.
- Preview numbers advance with Git commit height; ordinary pull requests do not require version-file edits.
- CI writes both `nbgvSemVer` and the effective `semVer` to `publish/version.json`.
- Local builds rely on the `Nerdbank.GitVersioning` MSBuild package to stamp the assembly; the running app reads `AssemblyInformationalVersion` at runtime when no CI-generated `version.json` file is present.
- CI/CD overrides `Version` / `InformationalVersion` during both direct `dotnet` builds and Docker-based `dotnet publish` so the published app reports the effective CI-selected version.
- The running app prefers a sibling `version.json` file (written during direct CI publish artifacts), otherwise checks CI-provided environment variables, and finally falls back to assembly-stamped version metadata.
- The shell script uses branch names like `feat/login` and `fix/login-bug`, with commit messages such as `feat(auth): ...` and `fix(auth): ...`.
- Do **not** assume commit messages or merge counts automatically change the preview number. Version changes are explicit and reviewed.

## Recommended strategy for agents

- Prefer **version + automatic commit height** for this repo, e.g. `1.0.0-preview.{height}`, `v1.0.0-rc.1`, then `v1.0.0`.
- This matches the chosen GitHub Flow model here: each commit within the planned target release gets a deterministic preview height.
- If the team wants `fix:` to become `0.1.1-preview.2` and `feat:` to become `0.2.0-preview.3`, that is a **custom policy**; mainstream .NET versioning tools do not provide that exact behavior out of the box.
- Keep one version engine across local and CI whenever possible. Mixing `nbgv` locally and GitVersion in CI makes version outcomes harder to reason about.

## Tool recommendation

- For .NET, prefer **Nerdbank.GitVersioning (`nbgv`)** if you want deterministic versions from Git history/tags and a repo-controlled `version.json`. It is already wired into this repo via the repo-root `dotnet-tools.json`.
- Prefer `nbgv` especially when the workflow is: merge to `main` → dev prerelease, tag RC for staging, tag stable for production.
- Use **GitVersion** only if you intentionally want CI-centric branch/tag inference and accept behavior that differs from local `nbgv` results.
- Neither `nbgv` nor GitVersion natively implements the exact `feat`/`fix` sequence `0.1.0-preview.1` → `0.1.1-preview.2` → `0.2.0-preview.3`; that requires extra automation or manual version bumps.

## Important caveats for agents

- The script `nerdbank-versioning.sh` is the practical versioning oracle in this repo: it builds a disposable Git repo, validates that ordinary merges preserve the explicit preview version, then checks RC/stable tag promotion behavior.
- There are no unit tests in this repo. For code changes, the practical validation path is `dotnet build`, and for versioning changes, restore tools and run `dotnet nbgv ...` inside a Git repo.
- RC/staging behavior is still tag-driven in CI/workflow logic; local `nbgv` output remains on the preview train unless you explicitly stabilize `version.json`.

## Change guidance

- Keep edits narrowly focused; most tasks should touch configuration/workflow files rather than app code.
- If you change version semantics, update both the local rules in `version.json` and the CI behavior in `.github/workflows/build-and-publish.yml`, or explicitly document why they remain different.
- Do not hand-edit `obj/` or `bin/`; they are generated outputs and only useful for confirming what the last build emitted.
