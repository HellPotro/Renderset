var builder = DistributedApplication.CreateBuilder(args);

// Conversión a PDF: Chromium headless detrás de una API HTTP. En Azure va
// como Container App aparte (ver BUNDLES.md); aquí, como contenedor local.
var gotenberg = builder.AddContainer("gotenberg", "gotenberg/gotenberg", "8")
    .WithHttpEndpoint(targetPort: 3000, name: "http")
    .WithHttpHealthCheck("/health");

var apiService = builder.AddProject<Projects.Renderset_ApiService>("apiservice")
    .WithHttpHealthCheck("/health")
    .WithEnvironment("Pdf__GotenbergUrl", gotenberg.GetEndpoint("http"))
    .WaitFor(gotenberg);

builder.AddProject<Projects.Renderset_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WithEnvironment("ReportingApi", apiService.GetEndpoint("https"))
    .WaitFor(apiService);

builder.Build().Run();
