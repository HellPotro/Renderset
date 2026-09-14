var builder = DistributedApplication.CreateBuilder(args);

var apiService = builder.AddProject<Projects.Renderset_ApiService>("apiservice")
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.Renderset_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WithEnvironment("ReportingApi", apiService.GetEndpoint("https"))
    .WaitFor(apiService);

builder.Build().Run();
