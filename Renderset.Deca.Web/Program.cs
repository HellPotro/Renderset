using System.Text.Json;
using System.Text.Json.Serialization;
using Refit;
using Renderset.Blazor.Components.Toasts;
using Renderset.Core.Services;
using Renderset.Deca.Web;
using Renderset.Deca.Web.Components;
using Renderset.Web.Components.Account;
using Renderset.Web.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

// Mismos usuarios y tenants que RenderSet Web (base de datos de RenderSet,
// ConnectionStrings:RenderSet), con su propia cookie. El propietario
// inicial lo crea Web, no esta aplicación.
builder.AddRendersetIdentity(options =>
{
    options.CookieName = "rs.deca.session";
    options.BootstrapOwner = false;
    options.ProductName = "RenderSet DeCA";
    options.ProductMark = "D";
});

// Cookies que sobreviven a un redespliegue (DataProtection:BlobUri).
builder.Services.AddRendersetDataProtection(builder.Configuration, "Renderset.Deca.Web");

builder.Services.AddScoped<IToastService, ToastService>();

// El diseñador del DeCA es el ReportEditor de RenderSet, que lo necesita.
builder.Services.AddSingleton<IReportConfigurationComposer, ReportConfigurationComposer>();

var reportingApiBaseAddress =
    new Uri(
        builder.Configuration["ReportingApi"]
        ?? throw new InvalidOperationException(
            "Falta 'ReportingApi': la URL de la API de RenderSet."));

// Clave de servicio propia del portal (Security:ServiceKeys "deca" en la
// API). En local la pasa Aspire (parámetro renderset-deca-service-key).
var reportingApiKey =
    builder.Configuration["ReportingApiKey"];

if (string.IsNullOrWhiteSpace(reportingApiKey))
{
    throw new InvalidOperationException(
        "Falta 'ReportingApiKey': la clave de servicio con la que el portal DeCA llama a la API.");
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

// Igual que en Web: la interfaz se crea por circuito con TenantTokenHandler
// delante, que firma cada llamada con el usuario y el tenant del circuito.
const string ReportingApiClient = "renderset-api";

builder.Services.AddHttpClient(ReportingApiClient, ConfigureReportingApi);

builder.Services.AddScoped<IDecaApi>(services =>
{
    var handler =
        new TenantTokenHandler(services.GetRequiredService<TenantTokenFactory>())
        {
            InnerHandler = services
                .GetRequiredService<IHttpMessageHandlerFactory>()
                .CreateHandler(ReportingApiClient)
        };

    var http = new HttpClient(handler, disposeHandler: false);
    ConfigureReportingApi(http);

    return RestService.For<IDecaApi>(http, refitSettings);
});

// Descarga del PDF a través del portal (/files/deca/...).
builder.Services.AddSingleton(
    new DecaFilesClient(
        reportingApiBaseAddress,
        reportingApiKey));

var app = builder.Build();

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
app.MapStaticAssets();

app.MapDecaFileEndpoints();
app.MapAccountEndpoints();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    // Entrar, invitación y contraseña: Renderset.Identity.
    .AddAdditionalAssemblies(typeof(AccountLinks).Assembly);

app.MapDefaultEndpoints();

app.Run();
