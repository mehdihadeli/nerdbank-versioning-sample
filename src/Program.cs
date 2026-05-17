var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet(
    "/",
    () =>
    {
        var version = versioning_samples.VersionInfoProvider.GetVersionInfo();

        return Results.Ok(
            new
            {
                message = "versioning-samples web app",
                versionEndpoint = "/version",
                version = version.SemVer,
                source = version.Source,
            }
        );
    }
);

app.MapGet("/version", () => Results.Ok(versioning_samples.VersionInfoProvider.GetVersionInfo()));

app.Run();

public partial class Program;
