namespace Renderset.Web.Identity;

/// <summary>
/// Primer propietario. Con el registro cerrado, alguien tiene que entrar el
/// primero: con Bootstrap:OwnerEmail, al arrancar se manda una invitación de
/// propietario al tenant de Bootstrap:TenantId (o Tenant:Id) si ese tenant
/// todavía no tiene propietario. Cuando ya lo tiene, no hace nada.
///
///     "Bootstrap": { "OwnerEmail": "tu@correo", "TenantId": "oranauto" },
///     "App": { "PublicUrl": "https://app.renderset.miempresa.com" }
/// </summary>
public sealed class BootstrapOwner(
    IServiceScopeFactory scopes,
    IConfiguration configuration,
    ILogger<BootstrapOwner> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        var email = configuration["Bootstrap:OwnerEmail"];
        var tenantId =
            string.IsNullOrWhiteSpace(configuration["Bootstrap:TenantId"])
                ? configuration["Tenant:Id"]
                : configuration["Bootstrap:TenantId"];
        var baseUrl = configuration["App:PublicUrl"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(tenantId))
            return;

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            logger.LogWarning(
                "Bootstrap:OwnerEmail está configurado pero falta App:PublicUrl (la URL de Web para el enlace de la invitación).");

            return;
        }

        try
        {
            await using var scope = scopes.CreateAsyncScope();

            var result =
                await scope.ServiceProvider
                    .GetRequiredService<TenantDirectory>()
                    .EnsureOwnerInvitationAsync(tenantId, email, baseUrl, stoppingToken);

            if (result is not null)
                logger.LogWarning("Bootstrap de {TenantId}: {Result}", tenantId, result);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Que falle (base de datos sin el script de Identity, correo sin
            // configurar) no puede impedir que Web arranque.
            logger.LogError(exception, "No se ha podido preparar el propietario inicial de {TenantId}.", tenantId);
        }
    }
}
