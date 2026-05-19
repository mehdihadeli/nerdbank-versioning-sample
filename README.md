# versioning-samples

A minimal ASP.NET Core sample that demonstrates:

- `Nerdbank.GitVersioning` as the canonical repo version engine
- GitHub Flow with Semantic Versioning: preview on `main` for dev, RC tags for staging, stable tags for production
- `dotnet nbgv tag` for release tags that match the local NBGV version
- passing CI-calculated version metadata into `dotnet publish` inside a Docker build
- exposing version metadata through a simple `/version` endpoint

## Correct versioning approach

The best practice for this sample is: **`version.json` is the source of truth, and CI must not invent a different RC or stable version.**

### Environment mapping (GitHub Flow)

- `dev`: push/merge to `main` while `version.json` is on `X.Y.Z-preview.{height}`
- `staging`: push tag `vX.Y.Z-rc.N` only after `version.json` on `main` is `X.Y.Z-rc.{height}`
- `production`: push tag `vX.Y.Z` only after `version.json` on `main` is `X.Y.Z`

### Invariant: local and CI versions must match

Before creating any RC or stable tag, local and CI should resolve the same semantic version from NBGV:

- local: `dotnet nbgv get-version -v SemVer2`
- CI: `dotnet nbgv get-version -v SemVer2` in workflow

If local says `1.0.0-preview.3`, tagging `v1.0.0-rc.1` is incorrect and CI should fail validation.

NBGV's CLI documentation describes release preparation as changing the version in `version.json` for the release/stabilization line, then creating a version tag from that NBGV-calculated version. In this repo's GitHub Flow variant, we keep that same principle but do it on `main` instead of maintaining long-lived release branches:

1. Normal development on `main` uses the next planned preview train, for example `1.0.0-preview.{height}`.
2. Each squash-merged PR increments the height: `1.0.0-preview.1`, `1.0.0-preview.2`, `1.0.0-preview.3`, ...
3. When the release is ready for staging, change `version.json` to `1.0.0-rc.{height}` and merge that change to `main`.
4. Local `dotnet nbgv get-version -v SemVer2` should now show `1.0.0-rc.1`.
5. Create the RC tag with `dotnet nbgv tag`, which creates `v1.0.0-rc.1`.
6. If an RC fix is needed, merge the fix to `main`; local NBGV reports `1.0.0-rc.2`, then `dotnet nbgv tag` creates `v1.0.0-rc.2`.
7. When ready for production, change `version.json` to `1.0.0`, merge it to `main`, verify local NBGV reports `1.0.0`, then run `dotnet nbgv tag` to create `v1.0.0`.
8. Immediately start the next train by changing `version.json` to the next planned version, for example `1.1.0-preview.{height}`.

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

```text
1.0.0-preview.3
1.0.0-rc.1
1.0.0
```

## Release flow commands

If your repository enforces "changes must be made through a pull request" on `main`, do not push version commits directly to `main`. Use a branch + PR, then tag from merged `main`.

For repeatable steps, use the helper script in this repo:

```bash
./release-version.sh --help
```

### Preview development

Keep `version.json` on the planned preview train during normal development:

```json
{
  "version": "1.0.0-preview.{height}"
}
```

A push to `main` builds and deploys to `dev` with the NBGV preview version.

### Enter RC / staging

Update `version.json` to the RC train and merge that change to `main` (this is what makes local and CI both report RC):

```json
{
  "version": "1.0.0-rc.{height}"
}
```

Prepare RC on a branch (recommended):

```bash
./release-version.sh prepare-staging 1.0.0 4
```

This creates a commit with `"version": "1.0.0-rc.4"` and verifies local `SemVer2` is `1.0.0-rc.4`.

Then:

```bash
git push origin HEAD:refs/heads/chore/rc4-version
# open PR to main and merge

git checkout main
git pull --ff-only origin main

./release-version.sh tag --push
```

The tag `v1.0.0-rc.4` deploys to `staging`. If another fix is merged while `version.json` remains on an RC train, run `./release-version.sh tag --push` again after the merge for the next RC value.

### Promote to production

Update `version.json` to the stable version and merge that change to `main`:

```json
{
  "version": "1.0.0"
}
```

Prepare production version on a branch:

```bash
./release-version.sh prepare-production 1.0.0
```

After PR merge to `main`, sync and tag:

```bash
git checkout main
git pull --ff-only origin main
./release-version.sh tag --push
```

The tag `v1.0.0` deploys to `production`.

### Start the next release train

After the production tag, prepare the next preview train on a branch:

```bash
./release-version.sh prepare-dev 1.1.0
```

Merge that PR to `main` so subsequent development returns to preview builds.

## Verify the sample strategy

The repository includes a disposable Git-history simulator that validates the preview → RC → stable → next-preview flow:

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

The Docker image does not generate a `version.json` file. Instead, the workflow passes the calculated version values into `dotnet publish` and also promotes them into container environment variables. The running container reads those CI-selected values first, then falls back to assembly metadata when no runtime override is present.

> Note: Docker image tags cannot contain the `+` used by SemVer build metadata. The workflow keeps build metadata in `InformationalVersion` and the app's `/version` payload, while publishing the container image with a Docker-safe tag if build metadata is ever present in the tag source.

## GitHub Actions behavior

- push to `main` -> dev version from `dotnet nbgv get-version -v SemVer2`, dev image, deploy to `dev`
- tag `vX.Y.Z-rc.N` -> staging image, deploy to `staging`, only if tag equals the NBGV version
- tag `vX.Y.Z` -> production image, deploy to `production`, only if tag equals the NBGV version

This keeps one version engine across environments: local and CI always agree on the semantic version.

The workflow pushes images to GitHub Container Registry using tags for both the environment and the calculated semantic version.
