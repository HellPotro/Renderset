namespace Renderset.Core.Sharing;

/// <summary>
/// Vista de ejemplo para previsualizar la configuración de la página pública
/// sin crear un bundle. Los documentos son marcadores: el visor se pinta
/// igual, pero el iframe queda en blanco.
/// </summary>
public static class DocumentBundlePreview
{
    public static DocumentBundleView Build(
        string tenantId,
        DocumentSharingSettings settings,
        DocumentBundleTexts texts,
        DateTime nowUtc,
        int fallbackExpirationDays)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(texts);

        var normalized = settings.Normalized();

        var days =
            normalized.DefaultExpirationDays
            ?? fallbackExpirationDays;

        string[] names =
        [
            "Factura F-2026-0412",
            "Packing list PL-4512",
            "Certificado de origen EUR.1"
        ];

        return new DocumentBundleView
        {
            Title = "Envío pedido 4512",
            Message = normalized.DefaultMessage,
            FooterText = normalized.FooterText,
            Culture = texts.Language,
            ExpiresAtUtc = nowUtc.AddDays(days),
            Branding = normalized.ToBranding(tenantId),
            Texts = texts,
            DownloadAllUrl = "#",
            DownloadAllPdfUrl = "#",
            PdfAvailable = true,
            Documents = names
                .Select((name, index) => new DocumentBundleViewItem
                {
                    Position = index + 1,
                    Title = name,
                    ViewUrl = "about:blank",
                    DownloadUrl = "#"
                })
                .ToList()
        };
    }
}
