using Refit;
using Renderset.Blazor.Components.Toasts;
using Renderset.Core.Services;
using Renderset.Core.Tenancy;
using Renderset.Web;
using Renderset.Web.Components;
using Renderset.Web.Tenancy;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddOutputCache();

builder.Services.AddScoped<ICurrentTenant, ConfiguredCurrentTenant>();
builder.Services.AddScoped<IToastService, ToastService>();
builder.Services.AddSingleton<IReportConfigurationComposer, ReportConfigurationComposer>();

var reportingApiBaseAddress =
    new Uri(builder.Configuration["ReportingApi"]!);

builder.Services
    .AddRefitClient<IRenderSetApi>()
    .ConfigureHttpClient(client =>
    {
        client.BaseAddress = reportingApiBaseAddress;
    });

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(
        "/Error",
        createScopeForErrors: true);

    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();
app.UseOutputCache();
app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();

app.Run();
