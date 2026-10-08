using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Renderset.Api.Endpoints;
using Renderset.Api.OpenApi;
using Renderset.Api.Security;
using Renderset.Api.Sharing;
using Renderset.Core.Blocks;
using Renderset.Core.Localization;
using Renderset.Core.Mapping;
using Renderset.Core.Presets;
using Renderset.Core.Reports;
using Renderset.Core.Rendering;
using Renderset.Core.Rendering.Pdf;
using Renderset.Core.Security;
using Renderset.Core.Services;
using Renderset.Core.Sharing;
using Renderset.Core.Tenancy;
using Renderset.Core.Themes;
using Renderset.Core.Translations;
using Renderset.Core.Variables;
using Renderset.Blazor.Rendering;
using Renderset.Deca;
using Renderset.Deca.Issuing;
using Renderset.Blazor.Sharing;
using Renderset.Infrastructure.Pdf;
using Renderset.Infrastructure.Persistence;
using Renderset.Infrastructure.Rendering;
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

// Resumen y descripción de cada operación: Renderset.Core.Api.ApiEndpointCatalog.
builder.Services.AddRendersetOpenApi();

builder.Services.AddDbContextFactory<RenderSetDbContext>(options =>
{
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("RenderSet"));
});

builder.Services.AddScoped<IReportBlockRepository, EfReportBlockRepository>();
builder.Services.AddScoped<IReportPresetRepository, EfReportPresetRepository>();
builder.Services.AddScoped<IReportThemeRepository, EfReportThemeRepository>();
builder.Services.AddScoped<IDataMappingRepository, EfDataMappingRepository>();
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

// ---------------------------------------------------------------- PDF

// Imágenes incrustadas al emitir: el documento no depende de la URL del logo.
var documentAssets =
    builder.Configuration
        .GetSection(DocumentAssetsOptions.SectionName)
        .Get<DocumentAssetsOptions>()
    ?? new DocumentAssetsOptions();

builder.Services.AddSingleton(documentAssets);
builder.Services.AddSingleton<HttpHtmlAssetInliner>();

// Los logos subidos a RenderSet (/assets) se leen de su almacén; el resto se
// descarga como siempre.
builder.Services.AddSingleton<IHtmlAssetInliner>(services =>
    new TenantAssetHtmlInliner(
        services.GetRequiredService<HttpHtmlAssetInliner>(),
        services.GetRequiredService<IServiceScopeFactory>(),
        services.GetRequiredService<TenantAssetOptions>(),
        documentAssets));

// Sin Pdf:GotenbergUrl la API arranca igual y sirve HTML; los botones de
// PDF no aparecen.
var pdf =
    builder.Configuration
        .GetSection(PdfOptions.SectionName)
        .Get<PdfOptions>()
    ?? new PdfOptions();

builder.Services.AddSingleton(pdf);

if (string.IsNullOrWhiteSpace(pdf.GotenbergUrl))
    builder.Services.AddSingleton<IPdfConverter, UnavailablePdfConverter>();
else
    builder.Services.AddSingleton<IPdfConverter>(new GotenbergPdfConverter(pdf));

builder.Services.AddScoped<IDocumentPdfService, DocumentPdfService>();

// ---------------------------------------------------------------- bundles

builder.Services.AddSingleton(TimeProvider.System);

var documentSharing =
    builder.Configuration
        .GetSection(DocumentSharingOptions.SectionName)
        .Get<DocumentSharingOptions>()
    ?? new DocumentSharingOptions();

builder.Services.AddSingleton(documentSharing);

// Dónde se abren los documentos emitidos (visor de Web): es el enlace que
// lleva el QR de la cabecera. En Aspire lo rellena AppHost.
//
// El QR lleva por defecto el enlace PÚBLICO (/d/{tenant}/{id}/{firma}), que
// abre cualquiera sin cuenta. Su dominio es el de los enlaces compartidos si
// no se indica otro, y su firma usa DocumentLinks:SigningKey o, si no hay,
// una clave derivada de la clave de servicio.
var documentLinks =
    builder.Configuration
        .GetSection(DocumentLinkOptions.SectionName)
        .Get<DocumentLinkOptions>()
    ?? new DocumentLinkOptions();

if (string.IsNullOrWhiteSpace(documentLinks.PublicBaseUrl))
    documentLinks.PublicBaseUrl = documentSharing.PublicBaseUrl;

documentLinks.UseSigningSecret(
    !string.IsNullOrWhiteSpace(documentLinks.SigningKey)
        ? documentLinks.SigningKey
        : builder.Configuration
            .GetSection(ApiSecurityOptions.SectionName)
            .Get<ApiSecurityOptions>()
            ?.ServiceKeys
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Key))
            ?.Key);

builder.Services.AddSingleton(documentLinks);

// Cifra el token de los enlaces para poder volver a abrirlos desde la
// gestión. El nombre de aplicación fijo hace que varias instancias de la API
// compartan claves si comparten carpeta.
// En Azure: DataProtection:BlobUri (+ KeyVaultKeyId); si no, la carpeta de
// DocumentSharing:DataProtectionKeysPath.
builder.Services.AddRendersetDataProtection(
    builder.Configuration,
    "Renderset.ApiService",
    documentSharing.DataProtectionKeysPath);

builder.Services.AddSingleton<IBundleTokenProtector, DataProtectionBundleTokenProtector>();

// ---------------------------------------------------------------- DeCA

// Dominio del QR (Deca:PublicBaseUrl): va impreso en cada DeCA durante un
// año, así que conviene uno corto y propio (deca.tudominio). Sin él se usa
// el de los enlaces compartidos. Tiene que llegar a esta API por HTTPS.
var deca =
    builder.Configuration
        .GetSection(DecaOptions.SectionName)
        .Get<DecaOptions>()
    ?? new DecaOptions();

if (string.IsNullOrWhiteSpace(deca.PublicBaseUrl))
    deca.PublicBaseUrl = documentSharing.PublicBaseUrl;

builder.Services.AddSingleton(deca);

// Base de datos propia (ConnectionStrings:Deca), aislada de la de RenderSet.
// Sin ella la API arranca igual y /api/deca y /q no existen.
var decaConnectionString =
    builder.Configuration.GetConnectionString("Deca");

var decaEnabled =
    !string.IsNullOrWhiteSpace(decaConnectionString);

if (decaEnabled)
{
    builder.Services.AddDbContextFactory<DecaDbContext>(options =>
    {
        options.UseSqlServer(decaConnectionString);
    });

    builder.Services.AddScoped<IDecaRepository, EfDecaRepository>();
    builder.Services.AddScoped<IDecaTemplateRepository, EfDecaTemplateRepository>();
    builder.Services.AddScoped<IDecaIssuer, DecaIssuer>();

    // PDF de los DeCA: en la base de datos Deca salvo que Deca:Storage diga
    // otra cosa (mismo formato que DocumentStorage, contenedor "deca" por
    // defecto). Su propio almacén, aparte de los documentos de RenderSet.
    var decaStorage =
        builder.Configuration
            .GetSection(DecaOptions.SectionName + ":Storage")
            .Get<DocumentStorageOptions>();

    switch (decaStorage?.Provider)
    {
        case DocumentStorageProvider.AzureBlob:
            if (string.IsNullOrWhiteSpace(builder.Configuration[DecaOptions.SectionName + ":Storage:ContainerName"]))
                decaStorage!.ContainerName = "deca";

            builder.Services.AddSingleton(
                new DecaContentStore(
                    AzureBlobDocumentContentStore.Create(decaStorage!)));
            break;

        case DocumentStorageProvider.FileSystem:
            builder.Services.AddSingleton(
                new DecaContentStore(
                    new FileSystemDocumentContentStore(
                        decaStorage!.RootPath!)));
            break;
    }
}

builder.Services.AddScoped<IDocumentBundleRepository, EfDocumentBundleRepository>();
builder.Services.AddScoped<IDocumentBundleService, DocumentBundleService>();
builder.Services.AddScoped<IDocumentBundleTextProvider, DocumentBundleTextProvider>();
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
            AzureBlobDocumentContentStore.Create(documentStorage));
        break;

    case DocumentStorageProvider.FileSystem:
        builder.Services.AddSingleton<IDocumentContentStore>(
            new FileSystemDocumentContentStore(
                documentStorage.RootPath!));
        break;
}

// Imágenes de los tenants (el logo subido). Van en la base de datos de
// RenderSet (TenantAssets) salvo que Assets:Storage diga otra cosa: mismo
// formato que DocumentStorage, contenedor "assets" por defecto.
var assetOptions =
    builder.Configuration
        .GetSection(TenantAssetOptions.SectionName)
        .Get<TenantAssetOptions>()
    ?? new TenantAssetOptions();

// Su dirección pública es la de la API: la misma que la de los enlaces
// compartidos o, si no hay, la del QR del DeCA.
if (string.IsNullOrWhiteSpace(assetOptions.PublicBaseUrl))
{
    assetOptions.PublicBaseUrl =
        !string.IsNullOrWhiteSpace(documentSharing.PublicBaseUrl)
            ? documentSharing.PublicBaseUrl
            : deca.PublicBaseUrl;
}

builder.Services.AddSingleton(assetOptions);

var assetStorage =
    builder.Configuration
        .GetSection(TenantAssetOptions.SectionName + ":Storage")
        .Get<DocumentStorageOptions>();

switch (assetStorage?.Provider)
{
    case DocumentStorageProvider.AzureBlob:
        if (string.IsNullOrWhiteSpace(builder.Configuration[TenantAssetOptions.SectionName + ":Storage:ContainerName"]))
            assetStorage!.ContainerName = "assets";

        builder.Services.AddSingleton(
            new TenantAssetContentStore(
                AzureBlobDocumentContentStore.Create(assetStorage!)));
        break;

    case DocumentStorageProvider.FileSystem:
        builder.Services.AddSingleton(
            new TenantAssetContentStore(
                new FileSystemDocumentContentStore(
                    assetStorage!.RootPath!)));
        break;
}

builder.Services.AddScoped<ITenantAssetRepository, EfTenantAssetRepository>();

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

// ---------------------------------------------------------------- seguridad

// Toda la API /api exige una clave: de tenant (el ERP de un cliente, sólo su
// tenant) o de servicio (RenderSet Web, cualquier tenant). /share sigue
// abierto: ahí la autorización es el token del enlace.
var security =
    builder.Configuration
        .GetSection(ApiSecurityOptions.SectionName)
        .Get<ApiSecurityOptions>()
    ?? new ApiSecurityOptions();

builder.Services.AddSingleton(security);
builder.Services.AddSingleton<ServiceKeyRegistry>();

// Token de usuario de Web: limita la clave de servicio al tenant y al rol
// del usuario que origina cada llamada. Obligatorio salvo en desarrollo.
var requireUserToken =
    security.RequireUserToken ?? !builder.Environment.IsDevelopment();

if (requireUserToken && string.IsNullOrWhiteSpace(security.UserTokenPublicKey))
{
    throw new InvalidOperationException(
        "Falta 'Security:UserTokenPublicKey' (clave pública de los tokens de usuario de Web). " +
        "Sin ella ninguna llamada de Web pasaría; para desarrollar sin tokens, Security:RequireUserToken = false.");
}

builder.Services.AddSingleton(
    new UserTokenSettings(
        string.IsNullOrWhiteSpace(security.UserTokenPublicKey)
            ? null
            : new TenantTokenValidator(security.UserTokenPublicKey),
        requireUserToken));
builder.Services.AddScoped<IApiKeyRepository, EfApiKeyRepository>();
builder.Services.AddScoped<IApiKeyService, ApiKeyService>();

builder.Services
    .AddAuthentication(ApiKeyAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
        ApiKeyAuthenticationHandler.SchemeName,
        configureOptions: null);

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(
        RendersetPolicies.ApiCaller,
        policy => policy.RequireAuthenticatedUser());

    options.AddPolicy(
        RendersetPolicies.ServiceOnly,
        policy => policy
            .RequireAuthenticatedUser()
            .RequireClaim(
                RendersetClaims.CallerKind,
                nameof(RendersetCallerKind.Service)));
});

var app = builder.Build();

// Lo primero del pipeline: una petición que el navegador abandona no es un
// error (ver UseClientAbortHandling).
app.UseClientAbortHandling();

if (app.Services.GetRequiredService<ServiceKeyRegistry>().HasKeys is false)
{
    app.Logger.LogWarning(
        "No hay ninguna clave de servicio configurada (Security:ServiceKeys). " +
        "RenderSet Web no podrá llamar a la API.");
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

// Un único grupo para toda la API privada: la clave y el control de tenant
// se aplican aquí y no endpoint a endpoint, para que un endpoint nuevo no
// pueda quedarse abierto por olvido.
var api =
    app.MapGroup(string.Empty)
        .RequireAuthorization(RendersetPolicies.ApiCaller)
        .AddEndpointFilter<TenantAccessFilter>();

api.MapAssignmentEndpoints();
api.MapPresetEndpoints();
api.MapThemeEndpoints();
api.MapDataMappingEndpoints();
api.MapReportBlockEndpoints();
api.MapReportEndpoints();
api.MapRenderEndpoints();
api.MapBundleEndpoints();
api.MapSharingSettingsEndpoints();
api.MapReportVariableEndpoints();
api.MapReportResourceEndpoints();
api.MapTenantCultureEndpoints();
api.MapApiKeyEndpoints();
api.MapTenantAssetEndpoints();
if (decaEnabled)
    api.MapDecaEndpoints();

// Público: enlaces compartidos con token propio.
app.MapSharingEndpoints();

// Público: las imágenes de los tenants (logo), por el hash de su contenido.
app.MapTenantAssetPublicEndpoints();

// El QR de la cabecera enlaza al visor de Web (/documents/{id}). Si
// DocumentLinks:BaseUrl apuntaba a la API en vez de a Web, los documentos
// emitidos llevan https://<api>/documents/{id}, que aquí no existía y daba
// una página en blanco. Con BaseUrl ya corregida se redirige al visor, así
// que los QR que ya están impresos siguen valiendo.
app.MapDocumentLinkRedirect();

// Público: el QR de cada DeCA descarga su PDF (/q/{código}).
if (decaEnabled)
    app.MapDecaPublicEndpoints();
else
    app.Logger.LogWarning(
        "DeCA desactivado: falta ConnectionStrings:Deca (base de datos de DeCA).");

app.MapDefaultEndpoints();

app.Run();
