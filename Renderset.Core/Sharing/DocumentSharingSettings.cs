using Renderset.Core.Rendering;
using Renderset.Core.Tenancy;

namespace Renderset.Core.Sharing;

/// <summary>
/// Configuración por tenant de la página pública de documentos compartidos:
/// marca, textos por defecto y caducidad por defecto.
///
/// Todo es opcional. Lo que no se configura cae a los valores de RenderSet
/// (colores por defecto, sin mensaje, caducidad de DocumentSharing).
///
/// El mensaje y el pie se aplican al pintar la página, no al crear el
/// bundle: el marco no forma parte del documento emitido, así que cambiar el
/// texto de contacto afecta también a los enlaces ya enviados. El mensaje
/// propio de un bundle, si lo tiene, siempre gana.
/// </summary>
public sealed class DocumentSharingSettings
{
    public const int MaxDefaultMessageLength = DocumentBundleService.MaxMessageLength;

    public const int MaxFooterTextLength = 500;

    public const int MaxLogoUrlLength = 500;

    /// <summary>
    /// Nombre del tenant. Sólo lectura: viene de la tabla Tenants y se
    /// ignora al guardar.
    /// </summary>
    public string? TenantName { get; set; }

    public string? LogoUrl { get; set; }

    public string? PrimaryColor { get; set; }

    public string? SecondaryColor { get; set; }

    /// <summary>
    /// Mensaje que se enseña cuando el bundle no trae uno propio.
    /// </summary>
    public string? DefaultMessage { get; set; }

    /// <summary>
    /// Texto al pie de la lista y del aviso de enlace caducado. Pensado para
    /// el contacto: "Dudas: logistica@oran.es · +34 942 000 000".
    /// </summary>
    public string? FooterText { get; set; }

    /// <summary>
    /// Caducidad de los bundles creados sin indicarla. Nulo usa
    /// DocumentSharing:DefaultExpirationDays.
    /// </summary>
    public int? DefaultExpirationDays { get; set; }

    /// <summary>
    /// Mismas reglas que se aplican al pintar (<see cref="TenantBranding"/>),
    /// pero aquí rechazando en vez de ignorar: quien configura tiene que
    /// saber que su color no se va a usar.
    /// </summary>
    public IReadOnlyList<RenderValidationError> Validate(
        int maxExpirationDays)
    {
        var errors = new List<RenderValidationError>();

        if (!string.IsNullOrWhiteSpace(LogoUrl) &&
            (LogoUrl.Trim().Length > MaxLogoUrlLength ||
             TenantBranding.CleanLogoUrl(LogoUrl) is null))
        {
            errors.Add(Error(
                "sharing.logo_invalid",
                "El logo tiene que ser una URL absoluta http o https.",
                "logoUrl"));
        }

        if (!string.IsNullOrWhiteSpace(PrimaryColor) &&
            TenantBranding.CleanColor(PrimaryColor) is null)
        {
            errors.Add(Error(
                "sharing.color_invalid",
                "El color principal tiene que ser hexadecimal: #00285A.",
                "primaryColor"));
        }

        if (!string.IsNullOrWhiteSpace(SecondaryColor) &&
            TenantBranding.CleanColor(SecondaryColor) is null)
        {
            errors.Add(Error(
                "sharing.color_invalid",
                "El color secundario tiene que ser hexadecimal: #E8EFF3.",
                "secondaryColor"));
        }

        if (DefaultMessage is { Length: > MaxDefaultMessageLength })
        {
            errors.Add(Error(
                "sharing.message_too_long",
                $"El mensaje por defecto no puede pasar de {MaxDefaultMessageLength} caracteres.",
                "defaultMessage"));
        }

        if (FooterText is { Length: > MaxFooterTextLength })
        {
            errors.Add(Error(
                "sharing.footer_too_long",
                $"El pie no puede pasar de {MaxFooterTextLength} caracteres.",
                "footerText"));
        }

        if (DefaultExpirationDays is { } days &&
            (days < 1 || days > maxExpirationDays))
        {
            errors.Add(Error(
                "sharing.expiration_out_of_range",
                $"La caducidad por defecto tiene que estar entre 1 y {maxExpirationDays} días.",
                "defaultExpirationDays"));
        }

        return errors;
    }

    /// <summary>
    /// Copia con los textos recortados y los vacíos convertidos en nulo, que
    /// es como se guarda: "sin configurar" se representa siempre igual.
    /// </summary>
    public DocumentSharingSettings Normalized() =>
        new()
        {
            TenantName = TenantName,
            LogoUrl = Clean(LogoUrl),
            PrimaryColor = Clean(PrimaryColor),
            SecondaryColor = Clean(SecondaryColor),
            DefaultMessage = Clean(DefaultMessage),
            FooterText = Clean(FooterText),
            DefaultExpirationDays = DefaultExpirationDays
        };

    public TenantBranding ToBranding(
        string tenantId) =>
        TenantBranding.Create(
            tenantId,
            TenantName,
            LogoUrl,
            PrimaryColor,
            SecondaryColor);

    private static string? Clean(
        string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();

    private static RenderValidationError Error(
        string code,
        string message,
        string path) =>
        new()
        {
            Code = code,
            Message = message,
            Path = path
        };
}


public interface IDocumentSharingSettingsRepository
{
    /// <summary>
    /// Nulo si el tenant no existe. Un tenant sin nada configurado devuelve
    /// una configuración vacía con su nombre.
    /// </summary>
    Task<DocumentSharingSettings?> GetAsync(
        string tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Falso si el tenant no existe.
    /// </summary>
    Task<bool> SaveAsync(
        string tenantId,
        DocumentSharingSettings settings,
        CancellationToken cancellationToken = default);
}
