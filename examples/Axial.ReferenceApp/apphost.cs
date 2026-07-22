#:sdk Aspire.AppHost.Sdk@13.4.6

// File-based AppHosts do not have a launchSettings.json. Supply the local dashboard
// endpoints here so `dotnet run --file apphost.cs` needs no separately-installed tooling.
Environment.SetEnvironmentVariable("ASPNETCORE_URLS", "http://localhost:18888");
Environment.SetEnvironmentVariable("ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL", "http://localhost:18889");
Environment.SetEnvironmentVariable("ASPIRE_RESOURCE_SERVICE_ENDPOINT_URL", "http://localhost:18890");
Environment.SetEnvironmentVariable("ASPIRE_ALLOW_UNSECURED_TRANSPORT", "true");
Environment.SetEnvironmentVariable("ASPIRE_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS", "true");

var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject("reference-app", "Axial.ReferenceApp.fsproj")
    .WithArgs("web")
    .WithHttpEndpoint(port: 5080, name: "http")
    .WithExternalHttpEndpoints();

builder.Build().Run();
