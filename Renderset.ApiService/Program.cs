using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;
using Renderset.Api.Endpoints;
using Renderset.Api.Sharing;
using Renderset.Core.Blocks;
using Renderset.Core.Localization;
using Renderset.Core.Presets;
using Renderset.Core.Reports;
using Renderset.Core.Rendering;
using Renderset.Core.Services;
using Renderset.Core.Sharing;
using Renderset.Core.Tenancy;
using Renderset.Core.Translations;
using Renderset.Core.Variables;
using Renderset.Blazor.Rendering;
using Renderset.Blazor.Sharing;
using Renderset.Infrastructure.Persistence;
using Renderset.Infrastructure.Repositories;
using Renderset.Infrastructure.Storage;
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
builder.Services.AddScoped<IReportVariableValues, ReportVariableValues>();
builder.Services.AddScoped<IReportVariableResolver, ReportVariableResolver>();
builder.Services.AddScoped<IReportResourceRepository, EfReportResourceRepository>();
builder.Services.AddScoped<ITenantCultureRepository, EfTenantCultureRepository>();
builder.Services.AddScoped<IReportTextCatalogFactory, ReportTextCatalogFactory>();
builder.Services.AddScoped<IRenderedDocumentRepository, EfRenderedDocumentRepository>();
builder.Services.AddScoped<IReportRenderService, ReportRenderService>();

// ---------------------------------------------------------------- bundles

builder.Services.AddSingleton(TimeProvider.System);

var documentSharing =
    builder.Configuration
        .GetSection(DocumentSharingOptions.SectionName)
        .Get<DocumentSharingOptions>()
    ?? new DocumentSharingOptions();

builder.Services.AddSingleton(documentSharing);

// Cifra el token de los enlaces para poder volver a abrirlos desde la
// gestión. El nombre de aplicación fijo hace que varias instancias de la API
// compartan claves si comparten carpeta.
var dataProtection =
    builder.Services
        .AddDataProtection()
        .SetApplicationName("Renderset.ApiService");

if (!string.IsNullOrWhiteSpace(documentSharing.DataProtectionKeysPath))
{
    dataProtection.PersistKeysToFileSystem(
        new DirectoryInfo(documentSharing.DataProtectionKeysPath));
}

builder.Services.AddSingleton<IBundleTokenProtector, DataProtectionBundleTokenProtector>();

builder.Services.AddScoped<IDocumentBundleRepository, EfDocumentBundleRepository>();
builder.Services.AddScoped<IDocumentBundleService, DocumentBundleService>();
builder.Services.AddScoped<IDocumentSharingSettingsRepository, EfDocumentSharingSettingsRepository>();
builder.Services.AddSingleton<IDocumentBundlePageRenderer, BlazorDocumentBundlePageRenderer>();

// Almacén del contenido de los documentos. Sin configurar (o con
// "Database") no se registra ninguno y el contenido sigue en la columna
// Content, como hasta ahora.
var documentStorage =
    builder.Configuration
        .GetSection(DocumentStorageOptions.SectionName)
        .Get<DocumentStorageOptions>()
    ?? new DocumentStorageOptions();

switch (documentStorage.Provider)
{
    case DocumentStorageProvider.AzureBlob:
        builder.Services.AddSingleton<IDocumentContentStore>(
            new AzureBlobDocumentContentStore(
                documentStorage.ConnectionString!,
                documentStorage.ContainerName));
        break;

    case DocumentStorageProvider.FileSystem:
        builder.Services.AddSingleton<IDocumentContentStore>(
            new FileSystemDocumentContentStore(
                documentStorage.RootPath!));
        break;
}

// El enlace público es lo único de la API abierto a cualquiera. El token no
// se puede adivinar, así que el límite no es contra fuerza bruta sino contra
// alguien que se ponga a pedir el ZIP en bucle. Detrás de un proxy hace
// falta UseForwardedHeaders para que la IP sea la del cliente y no la del
// proxy.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy(
        SharingEndpoints.RateLimitPolicy,
        context =>
            RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 120,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));
});

// El renderizador estático de Blazor no necesita estado por petición: pinta
// los mismos componentes que el preview del diseñador a partir del informe ya
// resuelto.
builder.Services.AddSingleton<IReportDocumentRenderer, BlazorReportDocumentRenderer>();

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

app.UseRateLimiter();

app.MapAssignmentEndpoints();
app.MapPresetEndpoints();
app.MapReportBlockEndpoints();
app.MapReportEndpoints();
app.MapRenderEndpoints();
app.MapBundleEndpoints();
app.MapSharingEndpoints();
app.MapSharingSettingsEndpoints();
app.MapReportVariableEndpoints();
app.MapReportResourceEndpoints();
app.MapTenantCultureEndpoints();

app.MapDefaultEndpoints();

app.Run();
