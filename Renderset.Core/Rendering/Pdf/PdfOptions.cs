namespace Renderset.Core.Rendering.Pdf;

/// <summary>
/// Sección "Pdf" de la configuración de la API.
///
///     "Pdf": {
///       "GotenbergUrl": "https://gotenberg.interno.azurecontainerapps.io",
///       "Username": "",
///       "Password": "",
///       "TimeoutSeconds": 60,
///       "MaxConcurrency": 4,
///       "Page": { "Paper": "A4", "ShowPageNumbers": true }
///     }
/// </summary>
public sealed class PdfOptions
{
    public const string SectionName = "Pdf";

    /// <summary>
    /// Vacío desactiva el PDF: la API sigue sirviendo HTML.
    /// </summary>
    public string? GotenbergUrl { get; set; }

    /// <summary>
    /// Credenciales de basic auth si Gotenberg se arranca con
    /// --api-enable-basic-auth. Gotenberg no tiene más autenticación, así que
    /// fuera de una red privada es obligatorio.
    /// </summary>
    public string? Username { get; set; }

    public string? Password { get; set; }

    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Conversiones simultáneas contra Gotenberg desde esta instancia. Cada
    /// una es un Chromium trabajando: sin límite, un "descargar todo" de un
    /// bundle grande puede tumbar el contenedor.
    /// </summary>
    public int MaxConcurrency { get; set; } = 4;

    public PdfPageOptions Page { get; set; } = new();
}
