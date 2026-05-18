# versioning-samples

A minimal ASP.NET Core sample that demonstrates:

- `Nerdbank.GitVersioning` for the canonical repo version
- CI-calculated effective versions for dev, staging, and production
- passing CI-calculated versions into `dotnet publish` inside a Docker build
- exposing version metadata through a simple `/version` endpoint

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

## Docker build with CI-style version injection

The Dockerfile accepts build arguments and forwards them into `dotnet publish`:

```powershell
docker build `
  --build-arg VERSION=1.0.0-preview.1.26139.12+abc1234 `
  --build-arg ASSEMBLY_VERSION=1.0.0.0 `
  --build-arg INFORMATIONAL_VERSION=1.0.0-preview.1.26139.12+abc1234 `
  --build-arg NBGV_SEMVER=1.0.0-preview.1 `
  --build-arg DATE_STAMP=26139 `
  --build-arg REVISION=12 `
  --build-arg COMMIT=abc1234 `
  --build-arg DEPLOYMENT_ENVIRONMENT=dev `
  -t versioning-samples:dev .
```

Inside the Docker build, these values are passed to:

```text
dotnet publish -p:Version=... -p:AssemblyVersion=... -p:InformationalVersion=...
```

The Docker image does not generate a `version.json` file. Instead, the workflow passes the calculated version values into `dotnet publish` and also promotes them into container environment variables. The running container reads those CI-selected values first, then falls back to assembly metadata when no runtime override is present.

> Note: Docker image tags cannot contain the `+` used by SemVer build metadata. The workflow keeps the full SemVer for `dotnet publish` and the app's `/version` payload, while publishing the container image with a Docker-safe tag variant that replaces `+` with `-`.

## GitHub Actions behavior

- push to `main` -> dev version, dev image, deploy to `dev`
- tag `vX.Y.Z-rc.N` -> staging image, deploy to `staging`
- tag `vX.Y.Z` -> production image, deploy to `production`

The workflow pushes images to GitHub Container Registry using tags for both the environment and the calculated semantic version.

