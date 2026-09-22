# versioning-samples

A minimal ASP.NET Core sample that demonstrates:

- `Nerdbank.GitVersioning` as the canonical repo version engine
- GitHub Flow with Semantic Versioning: explicit previews on `main`, RC tags for staging, stable tags for production
- `dotnet nbgv tag` for release tags that match the local NBGV version
- Release Drafter for one rolling preview draft and published RC/stable notes
- passing CI-calculated version metadata into `dotnet publish` inside a Docker build
- exposing version metadata through a simple `/version` endpoint

## Correct versioning approach

The best practice for this sample is: **`version.json` is the source of truth, and CI must not invent a different RC or stable version.**

### Environment mapping (GitHub Flow)

- `dev`: push/merge to `main` while `version.json` is on `X.Y.Z-preview.N`
- `staging`: push tag `vX.Y.Z-rc.N` only after `version.json` on `main` is `X.Y.Z-rc.N`
- `production`: push tag `vX.Y.Z` only after `version.json` on `main` is `X.Y.Z`

The CI workflow uses one publish path. It publishes the calculated preview
package and container from `main`, while RC and stable tags also publish the
matching Release Drafter release. Preview releases remain drafts; RC and stable
tags publish them.

### Preview version format in CI/CD

The canonical and effective preview version are the same explicit NBGV value:

```text
1.0.0-preview.1
```

The Git commit remains available in assembly informational metadata and the
container image digest. CI does not invent a second semantic version or append
a height/date suffix.

### Invariant: local and CI versions must match where promotion matters

Before creating any RC or stable tag, local and CI should resolve the same semantic version from NBGV:

- local: `dotnet nbgv get-version -v SemVer2`
- CI: `dotnet nbgv get-version -v SemVer2` in workflow

If local says `1.0.0-preview.3`, tagging `v1.0.0-rc.1` is incorrect and CI should fail validation.

For ordinary dev builds on `main`, CI publishes the committed preview version.
The preview number changes only when an intentional version PR runs
`prepare-preview`; ordinary PRs do not create new semantic versions.

NBGV's CLI documentation describes release preparation as changing the version in `version.json` for the release/stabilization line, then creating a version tag from that NBGV-calculated version. In this repo's GitHub Flow variant, we keep that same principle but do it on `main` instead of maintaining long-lived release branches:

1. Normal development on `main` uses the committed preview version, for example `1.0.0-preview.1`.
2. An intentional version PR runs `prepare-preview 1.0.0`, changing the version to `1.0.0-preview.2`.
3. When the release is ready for staging, change `version.json` to `1.0.0-rc.{height}` and merge that change to `main`.
4. Local `dotnet nbgv get-version -v SemVer2` should now show `1.0.0-rc.1`.
5. Create the RC tag with `dotnet nbgv tag`, which creates `v1.0.0-rc.1`.
6. If another RC is needed, run `prepare-rc 1.0.0` in a version PR; after merge, `dotnet nbgv tag` creates the next explicit RC tag.
7. When ready for production, change `version.json` to `1.0.0`, merge it to `main`, verify local NBGV reports `1.0.0`, then run `dotnet nbgv tag` to create `v1.0.0`.
8. Immediately start the next train by changing `version.json` to the next planned version, for example `1.1.0-preview.1`.

So yes: **if RC is the version you are releasing, you should be able to see RC locally with `dotnet nbgv get-version`.** A workflow where local NBGV still says `preview` but CI overrides a tag to `rc` is possible, but it is less clear and makes CI disagree with the repository's canonical version file.

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

## Check the local version

Run NBGV from the repository root:

```powershell
dotnet tool restore

dotnet nbgv get-version
dotnet nbgv get-version -v SemVer2
dotnet nbgv get-version -v AssemblyInformationalVersion
```

Expected examples:

````text
### Complete 1.0.0 release flow

The helper changes and validates `version.json`; it does not commit the file.
Use a branch name that describes real work, not the preview number. Commit each
version change in its work branch and pull request. Commands below assume Git
Bash and a clean working tree.

#### 1. Start preview.1

Create the first release-line version. The helper writes
`1.0.0-preview.1` because no `1.0.0-preview.N` exists yet:

```bash
git checkout -b feat/add-authentication
./release-version.sh prepare-preview 1.0.0
dotnet nbgv get-version -v SemVer2
# 1.0.0-preview.1
git add version.json
git commit -m "Start 1.0.0-preview.1"
git push origin HEAD:refs/heads/feat/add-authentication
````

Open and merge the pull request to `main`. CI publishes the preview package and
container to `dev`, and Release Drafter creates or updates draft
`v1.0.0-preview.1`. The GitHub release remains unpublished.

#### 2. Develop without changing the version

Merge ordinary feature, fix, test, and documentation pull requests while
`version.json` remains `1.0.0-preview.1`. Each merge rebuilds the same explicit
version and updates the same Release Drafter draft. Ordinary merges do not
become preview.2 automatically.

#### 3. Publish preview.2

When the next preview is intentionally ready, create another version PR:

```bash
git checkout main
git pull --ff-only origin main
git checkout -b feat/add-user-profile
./release-version.sh prepare-preview 1.0.0
dotnet nbgv get-version -v SemVer2
# 1.0.0-preview.2
git add version.json
git commit -m "Start 1.0.0-preview.2"
git push origin HEAD:refs/heads/feat/add-user-profile
```

Merge the PR to `main`. CI publishes `1.0.0-preview.2` to `dev` and updates
the draft.

#### 4. Publish preview.3

Repeat the version-PR flow when another preview is ready:

```bash
git checkout main
git pull --ff-only origin main
git checkout -b fix/token-validation
./release-version.sh prepare-preview 1.0.0
dotnet nbgv get-version -v SemVer2
# 1.0.0-preview.3
git add version.json
git commit -m "Start 1.0.0-preview.3"
git push origin HEAD:refs/heads/fix/token-validation
```

Merge the PR to publish `1.0.0-preview.3` to `dev`. Continue with the RC step
when preview validation is complete.

#### 5. Prepare and publish RC.1

When preview validation is complete, move the release intent to RC. The helper
changes `1.0.0-preview.2` to `1.0.0-rc.1`:

```bash
git checkout main
git pull --ff-only origin main
git checkout -b chore/update-ci-workflow
./release-version.sh prepare-rc 1.0.0
dotnet nbgv get-version -v SemVer2
# 1.0.0-rc.1
git add version.json
git commit -m "Start 1.0.0-rc.1"
git push origin HEAD:refs/heads/chore/update-ci-workflow
```

Merge the PR and validate the exact `main` commit. RC commits on `main` build
and test but do not deploy from `main`. Tag the approved commit:

```bash
git checkout main
git pull --ff-only origin main
./release-version.sh tag --push
# 1.0.0-rc.1
./release-version.sh tag --push
```

NBGV creates and pushes `v1.0.0-rc.1`; CI deploys it to `staging` and Release
Drafter publishes the matching release.

#### 6. Optional RC.2

If RC testing finds a problem, merge the fix normally, then create another
version PR. `prepare-rc` increments the current RC number:

```bash
git checkout main
git pull --ff-only origin main
git checkout -b fix/rc-logging
./release-version.sh prepare-rc 1.0.0
dotnet nbgv get-version -v SemVer2
# 1.0.0-rc.2
git add version.json
git commit -m "Start 1.0.0-rc.2"
git push origin HEAD:refs/heads/fix/rc-logging
```

After merging and validating that commit, run `./release-version.sh tag --push`
from `main` to publish `v1.0.0-rc.2` to `staging`.

#### 7. Prepare and publish stable 1.0.0

When the RC is accepted, remove the prerelease suffix through a reviewed PR:

```bash
git checkout main
git pull --ff-only origin main
git checkout -b chore/prepare-production-release
./release-version.sh prepare-stable 1.0.0
dotnet nbgv get-version -v SemVer2
# 1.0.0
git add version.json
git commit -m "Declare 1.0.0 stable"
git push origin HEAD:refs/heads/chore/prepare-production-release
```

Merge the PR, validate the exact commit, and create the stable tag:

```bash
git checkout main
git pull --ff-only origin main
```

# 1.0.0

./release-version.sh tag --push

````

NBGV creates and pushes `v1.0.0`; CI deploys it to `production` after the
GitHub Environment approval and Release Drafter publishes the stable release.

#### 8. Start the next release line

After stable publication, begin the next preview train through another PR:

```bash
git checkout -b feat/add-reporting
./release-version.sh prepare-preview 1.1.0
dotnet nbgv get-version -v SemVer2
# 1.1.0-preview.1
git add version.json
git commit -m "Start 1.1.0-preview.1"
git push origin HEAD:refs/heads/feat/add-reporting
````

Merge it to `main` before accepting work for the next release line.

## Verify the sample strategy

The repository includes a disposable Git-history simulator that validates NBGV
behavior for the preview → RC → stable flow. In the real workflow, preview
numbers advance through explicit version PRs rather than Git height:

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
- push to `main` + preview version -> dev image and deploy to `dev` using the explicit version `X.Y.Z-preview.N`
- push to `main` + RC/stable version -> CI only, `dev` deployment is skipped
- tag `vX.Y.Z-rc.N` -> staging image, deploy to `staging`, publish Release Drafter, only if tag equals NBGV version
- tag `vX.Y.Z` -> production image, deploy to `production`, publish Release Drafter, only if tag equals NBGV version

The single `build-and-publish.yml` workflow invokes Release Drafter with `publish: false`
for preview pushes and `publish: true` for RC/stable tags. Its
`if: always()` step preserves the draft even when image publication or another
release step fails; the workflow still fails and must be fixed and rerun.

This keeps one version engine across environments: local and CI always agree on the semantic version.

The workflow pushes images to GitHub Container Registry using tags for both the environment and the calculated semantic version.
