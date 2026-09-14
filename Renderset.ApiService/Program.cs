using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Renderset.Api.Endpoints;
using Renderset.Core.Blocks;
using Renderset.Core.Localization;
using Renderset.Core.Presets;
using Renderset.Core.Reports;
using Renderset.Core.Services;
using Renderset.Core.Translations;
using Renderset.Core.Variables;
using Renderset.Infrastructure.Persistence;
using Renderset.Infrastructure.Repositories;
using Renderset.Infrastructure.Translations;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(
        new JsonStringEnumConverter());
});

builder.Services.AddOpenApi();

builder.Services.AddDbContextFactory<RenderSetDbContext>(options =>
{
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("RenderSet"));
});

builder.Services.AddScoped<IReportBlockRepository, EfReportBlockRepository>();
builder.Services.AddScoped<IReportPresetRepository, EfReportPresetRepository>();
builder.Services.AddScoped<IReportPresetAssignmentRepository, EfReportPresetAssignmentRepository>();
builder.Services.AddScoped<IReportPresetProvider, ReportPresetProvider>();
builder.Services.AddScoped<IReportRepository, EfReportRepository>();
builder.Services.AddScoped<IReportVariableRepository, EfReportVariableRepository>();
builder.Services.AddScoped<IReportVariableResolver, ReportVariableResolver>();
builder.Services.AddScoped<IReportResourceRepository, EfReportResourceRepository>();
builder.Services.AddScoped<ITenantCultureRepository, EfTenantCultureRepository>();
builder.Services.AddScoped<IReportTextCatalogFactory, ReportTextCatalogFactory>();

builder.Services.Configure<AzureTranslatorOptions>(
    builder.Configuration.GetSection(AzureTranslatorOptions.SectionName));

builder.Services.AddHttpClient<ITranslationService, AzureTranslationService>((serviceProvider, client) =>
{
    var options =
        serviceProvider
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<AzureTranslatorOptions>>()
            .Value;

    client.BaseAddress =
        new Uri(options.Endpoint.TrimEnd('/'));
});

builder.Services.AddSingleton<IReportConfigurationComposer, ReportConfigurationComposer>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapAssignmentEndpoints();
app.MapPresetEndpoints();
app.MapReportBlockEndpoints();
app.MapReportEndpoints();
app.MapReportVariableEndpoints();
app.MapReportResourceEndpoints();
app.MapTenantCultureEndpoints();

app.MapDefaultEndpoints();

app.Run();
