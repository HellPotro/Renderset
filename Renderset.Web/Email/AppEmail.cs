using System.Net;
using Azure;
using Azure.Communication.Email;
using Azure.Identity;
using Renderset.Core.Tenancy;

namespace Renderset.Web.Email;

/// <summary>
/// Envío de correos de Web: invitaciones y restablecer contraseña.
/// </summary>
public interface IAppEmailSender
{
    Task SendAsync(
        string to,
        string subject,
        string html,
        CancellationToken cancellationToken = default);
}


/// <summary>
/// Sección "Email" de la configuración.
///
///     "Email": {
///       "Sender": "DoNotReply@xxxx.azurecomm.net",
///       "Endpoint": "https://xxxx.communication.azure.com"   ← con identidad administrada
///       "ConnectionString": "endpoint=…;accesskey=…"         ← o con clave (user-secrets / Key Vault)
///     }
/// </summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string? Sender { get; set; }

    public string? Endpoint { get; set; }

    public string? ConnectionString { get; set; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Sender) &&
        (!string.IsNullOrWhiteSpace(Endpoint) || !string.IsNullOrWhiteSpace(ConnectionString));
}


/// <summary>
/// Azure Communication Services Email. En Azure, mejor con identidad
/// administrada (Endpoint) que con la cadena con clave.
/// </summary>
public sealed class AcsEmailSender(
    EmailOptions options,
    ILogger<AcsEmailSender> logger)
    : IAppEmailSender
{
    private readonly EmailClient _client =
        string.IsNullOrWhiteSpace(options.ConnectionString)
            ? new EmailClient(new Uri(options.Endpoint!), new DefaultAzureCredential())
            : new EmailClient(options.ConnectionString);

    public async Task SendAsync(
        string to,
        string subject,
        string html,
        CancellationToken cancellationToken = default)
    {
        var message =
            new EmailMessage(
                options.Sender!,
                to,
                new EmailContent(subject) { Html = html });

        // Started y no Completed: no hace falta esperar a la entrega para
        // contestar al usuario, y ACS reintenta por su cuenta.
        var operation =
            await _client.SendAsync(
                WaitUntil.Started,
                message,
                cancellationToken);

        logger.LogInformation(
            "Correo '{Subject}' enviado a {To} (operación {OperationId}).",
            subject,
            to,
            operation.Id);
    }
}


/// <summary>
/// Sin ACS configurado (desarrollo): el correo va al log, con el enlace,
/// para poder probar invitaciones y contraseñas sin servicio de correo.
/// </summary>
public sealed class LoggingEmailSender(
    ILogger<LoggingEmailSender> logger)
    : IAppEmailSender
{
    public Task SendAsync(
        string to,
        string subject,
        string html,
        CancellationToken cancellationToken = default)
    {
        logger.LogWarning(
            "Email sin configurar: no se envía '{Subject}' a {To}. Contenido:\n{Html}",
            subject,
            to,
            html);

        return Task.CompletedTask;
    }
}


/// <summary>
/// HTML de los correos. Sencillo y con estilos en línea: es lo único que
/// respetan todos los clientes de correo.
/// </summary>
public static class EmailTemplates
{
    public static string Invitation(
        string tenantName,
        string invitedBy,
        TenantRole role,
        string link,
        TimeSpan lifetime) =>
        Layout(
            $"Te han invitado a {Encode(tenantName)}",
            $"""
            <p>{Encode(invitedBy)} te ha invitado a <strong>{Encode(tenantName)}</strong> en RenderSet
            como <strong>{RoleName(role)}</strong>.</p>
            <p>Para entrar, acepta la invitación y elige tu contraseña:</p>
            {Button("Aceptar la invitación", link)}
            <p style="color:#6d7b83;font-size:12px">El enlace caduca en {lifetime.TotalDays:0} días y sólo sirve una vez.
            Si no esperabas este correo, ignóralo.</p>
            """);

    public static string PasswordReset(
        string link) =>
        Layout(
            "Restablecer la contraseña",
            $"""
            <p>Alguien (seguramente tú) ha pedido restablecer la contraseña de tu cuenta de RenderSet.</p>
            {Button("Elegir una contraseña nueva", link)}
            <p style="color:#6d7b83;font-size:12px">Si no lo has pedido tú, ignora este correo: tu contraseña no cambia.</p>
            """);

    public static string RoleName(
        TenantRole role) =>
        role switch
        {
            TenantRole.Owner => "propietario",
            TenantRole.Admin => "administrador",
            _ => "miembro"
        };

    private static string Button(
        string text,
        string link) =>
        $"""
        <p style="margin:24px 0">
          <a href="{Encode(link)}" style="display:inline-block;padding:11px 18px;border-radius:6px;background:#00285A;color:#ffffff;text-decoration:none;font-weight:600">{Encode(text)}</a>
        </p>
        <p style="color:#6d7b83;font-size:12px;word-break:break-all">O copia este enlace: {Encode(link)}</p>
        """;

    private static string Layout(
        string title,
        string body) =>
        $"""
        <!DOCTYPE html>
        <html lang="es">
        <body style="margin:0;padding:24px;background:#f3f5f7;font-family:Segoe UI,Arial,sans-serif;color:#25313a">
          <div style="max-width:560px;margin:0 auto;padding:28px;border-radius:10px;background:#ffffff;border-top:4px solid #00285A">
            <h1 style="margin:0 0 16px;font-size:20px">{title}</h1>
            {body}
            <p style="margin-top:28px;color:#9aa9b1;font-size:11px">RenderSet</p>
          </div>
        </body>
        </html>
        """;

    private static string Encode(
        string value) =>
        WebUtility.HtmlEncode(value);
}
