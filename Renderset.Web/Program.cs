using System.Text.Json;
using System.Text.Json.Serialization;
using Refit;
using Renderset.Blazor.Components.Toasts;
using Renderset.Core.Services;
using Renderset.Core.Tenancy;
using Renderset.Web;
using Renderset.Web.Components;
using Renderset.Web.Files;
using Renderset.Web.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Lo que se pega en un textarea (filas de SSMS en Datos, Generar o Nuevo
// report) viaja entero por SignalR en el evento onchange. El límite por
// defecto de 32 KB se queda en unas 60-70 filas de un SELECT ancho y, al
// pasarlo, el servidor corta el circuito sin más aviso que la reconexión.
// 4 MB da para unas 10.000 filas; ajustable en Blazor:MaximumReceiveMessageSizeKb.
var maximumReceiveMessageSize =
    builder.Configuration.GetValue<int?>("Blazor:MaximumReceiveMessageSizeKb") * 1024
    ?? 4 * 1024 * 1024;

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddHubOptions(options =>
        options.MaximumReceiveMessageSize = maximumReceiveMessageSize);

builder.Services.AddOutputCache();

// Usuarios, sesión, tenant del usuario, roles, correo y token para la API.
builder.AddRendersetIdentity();

// Cookies de sesión que sobreviven a un redespliegue (DataProtection:BlobUri).
builder.Services.AddRendersetDataProtection(builder.Configuration, "Renderset.Web");

builder.Services.AddScoped<IToastService, ToastService>();
builder.Services.AddSingleton<IReportConfigurationComposer, ReportConfigurationComposer>();

var reportingApiBaseAddress =
    new Uri(builder.Configuration["ReportingApi"]!);

// Clave de servicio con la que Web llama a la API. Es un secreto: en local
// la pasa Aspire (parámetro renderset-service-key) o user-secrets; en Azure,
// Key Vault. Sin ella la API rechaza todas las llamadas, así que es mejor
// no arrancar que arrancar rota.
var reportingApiKey =
    builder.Configuration["ReportingApiKey"];

if (string.IsNullOrWhiteSpace(reportingApiKey))
{
    throw new InvalidOperationException(
        "Falta 'ReportingApiKey': la clave de servicio con la que Web llama a la API.");
}

void ConfigureReportingApi(HttpClient client)
{
    client.BaseAddress = reportingApiBaseAddress;
    client.DefaultRequestHeaders.Add("X-Api-Key", reportingApiKey);
}

var refitSettings =
    new RefitSettings
    {
        ContentSerializer =
            new SystemTextJsonContentSerializer(
                new JsonSerializerOptions(JsonSerializerDefaults.Web)
                {
                    PropertyNameCaseInsensitive = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                    Converters = { new JsonStringEnumConverter() }
                })
    };

// Cliente con nombre: lleva la resiliencia y el descubrimiento de servicios
// de ServiceDefaults. Sobre él, la interfaz de Refit se crea por circuito
// con TenantTokenHandler delante, que firma cada llamada con el usuario y el
// tenant de ese circuito: un handler de la factoría vive en otro ámbito y
// no los vería.
const string ReportingApiClient = "renderset-api";

builder.Services.AddHttpClient(ReportingApiClient, ConfigureReportingApi);

builder.Services.AddScoped<IRenderSetApi>(services =>
{
    var handler =
        new TenantTokenHandler(services.GetRequiredService<TenantTokenFactory>())
        {
            InnerHandler = services
                .GetRequiredService<IHttpMessageHandlerFactory>()
                .CreateHandler(ReportingApiClient)
        };

    // disposeHandler: false; la cadena de la factoría es compartida y la
    // libera la factoría.
    var http = new HttpClient(handler, disposeHandler: false);
    ConfigureReportingApi(http);

    return RestService.For<IRenderSetApi>(http, refitSettings);
});

// Cliente para servir documentos y PDF a través de Web (/files/...). Propio
// y no de IHttpClientFactory: los de la factoría heredan la resiliencia de
// ServiceDefaults, que corta a los 10 segundos, y generar un PDF largo la
// primera vez puede tardar más.
builder.Services.AddSingleton(
    new DocumentFilesClient(
        reportingApiBaseAddress,
        reportingApiKey));

var app = builder.Build();

// Lo primero del pipeline: una petición que el navegador abandona no es un
// error (ver UseClientAbortHandling).
app.UseClientAbortHandling();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(
        "/Error",
        createScopeForErrors: true);

    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.UseOutputCache();
app.MapStaticAssets();

app.MapDocumentFileEndpoints();
app.MapAccountEndpoints();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();

app.Run();
