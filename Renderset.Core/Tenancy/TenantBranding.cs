using System.Text.RegularExpressions;

namespace Renderset.Core.Tenancy;

/// <summary>
/// Marca del tenant para las páginas que ve su cliente: el visor de
/// documentos compartidos y sus avisos. Los documentos llevan ya su propio
/// tema dentro; esto es sólo el marco que los rodea.
///
/// Los valores se limpian al construirlo y no al pintarlo: un color acaba
/// dentro de un bloque CSS y un logo dentro de un atributo src, y en los dos
/// sitios un valor arbitrario es una forma de inyectar contenido en una
/// página pública.
/// </summary>
public sealed partial class TenantBranding
{
    public const string DefaultPrimaryColor = "#00285A";

    public const string DefaultSecondaryColor = "#E8EFF3";

    public required string TenantId { get; init; }

    public required string DisplayName { get; init; }

    public string? LogoUrl { get; init; }

    public string PrimaryColor { get; init; } = DefaultPrimaryColor;

    public string SecondaryColor { get; init; } = DefaultSecondaryColor;

    public static TenantBranding Create(
        string tenantId,
        string? displayName,
        string? logoUrl,
        string? primaryColor,
        string? secondaryColor)
    {
        return new TenantBranding
        {
            TenantId = tenantId,
            DisplayName =
                string.IsNullOrWhiteSpace(displayName)
                    ? tenantId
                    : displayName.Trim(),
            LogoUrl = CleanLogoUrl(logoUrl),
            PrimaryColor = CleanColor(primaryColor) ?? DefaultPrimaryColor,
            SecondaryColor = CleanColor(secondaryColor) ?? DefaultSecondaryColor
        };
    }

    /// <summary>
    /// Marco sin marca, para cuando no se puede saber de quién es el enlace
    /// (token incorrecto): no se le enseña a un desconocido de qué empresa
    /// era el enlace que estaba probando.
    /// </summary>
    public static TenantBranding Neutral() =>
        new()
        {
            TenantId = string.Empty,
            DisplayName = "RenderSet"
        };

    public static string? CleanColor(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();

        return HexColor().IsMatch(trimmed)
            ? trimmed
            : null;
    }

    /// <summary>
    /// Sólo URLs absolutas http(s). Un "javascript:" o un "data:" con HTML no
    /// llegan nunca al atributo.
    /// </summary>
    public static string? CleanLogoUrl(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri))
            return null;

        return uri.Scheme == Uri.UriSchemeHttps ||
               uri.Scheme == Uri.UriSchemeHttp
            ? uri.AbsoluteUri
            : null;
    }

    [GeneratedRegex("^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6})$")]
    private static partial Regex HexColor();
}
