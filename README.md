# versioning-samples

A minimal ASP.NET Core sample that demonstrates:

- `Nerdbank.GitVersioning` as the canonical repo version engine
- GitHub Flow with Semantic Versioning: commit-height previews on `main`, RC tags for staging, stable tags for production
- `vX.Y.Z-rc.N` and `vX.Y.Z` tags for RC and stable releases
- Release Drafter for one rolling preview draft and published RC/stable notes
- passing CI-calculated version metadata into `dotnet publish` inside a Docker build
- exposing version metadata through a simple `/version` endpoint

## Correct versioning approach

The best practice for this sample is: **`version.json` is the source of truth, and CI must not invent a different RC or stable version.**

### Environment mapping (GitHub Flow)

- `dev`: push/merge to `main` while `version.json` is on `X.Y.Z-preview.{height}`
- `staging`: push tag `vX.Y.Z-rc.N` only after `version.json` on `main` is `X.Y.Z-rc.{height}`
- `production`: push tag `vX.Y.Z` only after `version.json` on `main` is `X.Y.Z`

The CI workflow uses one publish path. It publishes the calculated preview
package and container from `main`, while RC and stable tags also publish the
matching Release Drafter release. Preview releases remain drafts; RC and stable
tags publish them.

### Preview version format in CI/CD

The canonical and effective preview version are the same NBGV value calculated
from Git commit height:

```text
1.0.0-preview.5
```

The Git commit remains available in assembly informational metadata and the
container image digest. CI does not invent a second semantic version or append
a height/date suffix.

### Invariant: local and CI versions must match where promotion matters

Before creating an RC or stable tag, verify the current `main` commit locally:

```bash
dotnet nbgv get-version -v SemVer2
```

The preview number is calculated automatically from Git commit height. Do not
edit `version.json` to change `preview.1` to `preview.2`. Only change the base
release train, such as `1.0.0-preview.{height}` to `1.1.0-preview.{height}`.

The initial preview configuration scopes its `-1` height offset to
`1.0.0-preview.{height}`. When the RC train changes the version to
`1.0.0-rc.{height}`, that preview-only offset no longer applies. The RC train
commit therefore becomes `1.0.0-rc.1`, and the first RC-fix commit becomes
`1.0.0-rc.2`.

`publicReleaseRefSpec` identifies public release refs; it does not make NBGV
infer a new version from an arbitrary tag. `nbgv get-version` still calculates
from `version.json` and Git height. Therefore, change the version template to
`X.Y.Z-rc.{height}` before RC tags and to `X.Y.Z` before a stable tag. CI
compares each release tag with NBGV's calculated version and rejects a mismatch.

## Release scenario

`main` is protected. Every change reaches it through a short-lived feature or
release-preparation branch, typically as an approved squash merge. Release
tags are created only after the merge has completed. The comments show the
expected semantic version after each merge or tag:

```bash
# First feature: merge to main produces 1.0.0-preview.1.
git switch main
git pull --ff-only
git switch -c feature/customer-export
git add -A
git commit -m "feat: add customer export"
git switch main
git merge --squash feature/customer-export
git commit -m "feat: add customer export"
git branch -d feature/customer-export
# Version produced on main: 1.0.0-preview.1

# Second feature: merge to main produces 1.0.0-preview.2.
git switch -c feature/add-auth
git add -A
git commit -m "feat: add authentication"
git switch main
git merge --squash feature/add-auth
git commit -m "feat: add authentication"
git branch -d feature/add-auth
# Version produced on main: 1.0.0-preview.2

# First RC: the release-train commit calculates 1.0.0-rc.1.
git switch -c chore/prepare-1.0.0-rc
dotnet nbgv set-version "1.0.0-rc.{height}"
git add version.json
git commit -m "chore: prepare 1.0.0 RC train"
git switch main
git merge --squash chore/prepare-1.0.0-rc
git commit -m "chore: prepare 1.0.0 RC train"
git branch -d chore/prepare-1.0.0-rc
version="$(dotnet nbgv get-version -v SemVer2)"
# Version produced on main: 1.0.0-rc.1
dotnet nbgv tag
git push origin "v$version"
# Tag created and pushed: v1.0.0-rc.1

# Helper equivalent: run this on the release-preparation branch instead of the
# NBGV set-version command above.
# ./release-version.sh prepare-rc 1.0.0
# git add version.json && git commit -m "chore: prepare 1.0.0 RC train"
# version="$(dotnet nbgv get-version -v SemVer2)"
# ./release-version.sh tag
# git push origin "v$version"

# RC fix: merge to main produces 1.0.0-rc.2.
git switch -c fix/release-candidate
git add -A
git commit -m "fix: correct release candidate behavior"
git switch main
git merge --squash fix/release-candidate
git commit -m "fix: correct release candidate behavior"
git branch -d fix/release-candidate
# Version produced on main: 1.0.0-rc.2
version="$(dotnet nbgv get-version -v SemVer2)"
dotnet nbgv tag
git push origin "v$version"
# Tag created and pushed: v1.0.0-rc.2

# Helper equivalent: no prepare-rc command is needed for later RCs.
# ./release-version.sh tag
# git push origin "v$version"

# 1.0.0
git switch -c chore/prepare-1.0.0
dotnet nbgv set-version 1.0.0
git add version.json
git commit -m "chore: prepare 1.0.0"
git switch main
git merge --squash chore/prepare-1.0.0
git commit -m "chore: prepare 1.0.0"
git branch -d chore/prepare-1.0.0
# Version produced on main: 1.0.0
dotnet nbgv tag
git push origin v1.0.0
# Tag created and pushed: v1.0.0
# Helper equivalent: run this on the release-preparation branch instead of the
# NBGV set-version command above.
# ./release-version.sh prepare-stable 1.0.0
# git add version.json && git commit -m "chore: prepare 1.0.0"
# ./release-version.sh tag

# Start the 1.1.0 preview train. The train commit calculates preview.0.
git switch -c chore/prepare-1.1.0-preview
dotnet nbgv set-version "1.1.0-preview.{height}"
# Helper equivalent: use this on the release-preparation branch instead of the
# NBGV command above.
# ./release-version.sh prepare-train 1.1.0
git add version.json
git commit -m "chore: start 1.1.0 preview train"
git switch main
git merge --squash chore/prepare-1.1.0-preview
git commit -m "chore: start 1.1.0 preview train"
git branch -d chore/prepare-1.1.0-preview
# Version produced on main: 1.1.0-preview.0

# First feature in the new train: merge to main produces 1.1.0-preview.1.
git switch -c feature/add-authorization
git add -A
git commit -m "feat: add authorization"
git switch main
git merge --squash feature/add-authorization
git commit -m "feat: add authorization"
git branch -d feature/add-authorization
# Version produced on main: 1.1.0-preview.1
```

`release-version.sh` is optional shorthand for NBGV's release workflow. Its
`prepare-train`, `prepare-rc`, and `prepare-stable` commands update
`version.json`; you still review, commit, and push those changes. Its `tag`
command runs `dotnet nbgv tag` for the already-committed version and leaves the
final `git push` under your control.

## Endpoints

- `GET /` - basic app information
- `GET /version` - effective version metadata

## Local run

```powershell
dotnet tool restore

dotnet build .\src\versioning-samples.csproj

dotnet run --project .\src\versioning-samples.csproj
```

Then open:

```text
http://localhost:5000/version
```

## Run tests

```powershell
dotnet test .\tests\versioning-samples.Tests\versioning-samples.Tests.csproj
```

Two end-to-end tests exercise the full preview → RC → stable sequence in
temporary Git repositories: one uses NBGV commands directly and one uses
`release-version.sh`. They check NBGV's calculated SemVer separately from the
tag created at `HEAD`. A regression test also creates a mismatched `v9.9.9`
tag on a preview commit and verifies that NBGV still reports the version
calculated from `version.json` and Git height.

## Check the local version

Run NBGV from the repository root:

```powershell
dotnet tool restore

dotnet nbgv get-version
dotnet nbgv get-version -v SemVer2
dotnet nbgv get-version -v AssemblyInformationalVersion
```

The previous explicit-counter walkthrough has been removed. Preview numbers now
come from Git commit height, and RC/stable releases use `vX.Y.Z-rc.N` or
`vX.Y.Z` tags as shown in the release scenario above.

## Verify the sample strategy

The repository includes a disposable Git-history simulator that validates NBGV
behavior for the preview → RC → stable flow. Preview numbers advance through
Git commit height, not explicit version PRs:

```bash
./nerdbank-versioning.sh
```

## Docker build with CI-style version injection

The Dockerfile accepts build arguments and forwards them into `dotnet publish`:

```powershell
docker build `
  --build-arg VERSION=1.0.0-rc.1 `
  --build-arg ASSEMBLY_VERSION=1.0.0.0 `
  --build-arg INFORMATIONAL_VERSION=1.0.0-rc.1+abc1234 `
  --build-arg NBGV_SEMVER=1.0.0-rc.1 `
  --build-arg DATE_STAMP=26139 `
  --build-arg REVISION=12 `
  --build-arg COMMIT=abc1234 `
  --build-arg DEPLOYMENT_ENVIRONMENT=staging `
  -t versioning-samples:staging .
```

Inside the Docker build, these values are passed to:

```text
dotnet publish -p:Version=... -p:AssemblyVersion=... -p:InformationalVersion=...
```

The Docker image does not generate a second semantic version. The workflow passes
the calculated version values into `dotnet publish` and also promotes them into
container environment variables. The running container reads those CI-selected
values first, then falls back to assembly metadata when no runtime override is
present.

## GitHub Actions behavior

- push to `main` -> CI validation always runs with base version from `dotnet nbgv get-version -v SemVer2`
- push to `main` + preview version -> dev image and deploy to `dev` using the calculated version `X.Y.Z-preview.{height}`
- push to `main` + RC/stable version -> CI only, `dev` deployment is skipped
- tag `vX.Y.Z-rc.N` -> staging image, deploy to `staging`, publish Release Drafter, only if tag equals NBGV version
- tag `vX.Y.Z` -> production image, deploy to `production`, publish Release Drafter, only if tag equals NBGV version

The single `build-and-publish.yml` workflow invokes Release Drafter with `publish: false`
for preview pushes and `publish: true` for RC/stable tags. Its
`if: always()` step preserves the draft even when image publication or another
release step fails; the workflow still fails and must be fixed and rerun.

This keeps one version engine across environments: local and CI always agree on the semantic version.

The workflow pushes images to GitHub Container Registry using tags for both the environment and the calculated semantic version.
