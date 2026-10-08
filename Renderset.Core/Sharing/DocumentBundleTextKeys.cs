namespace Renderset.Core.Sharing;

/// <summary>
/// Claves de los textos de la página pública en el Diccionario (ámbito
/// <see cref="Localization.ReportResourceScope.Sharing"/>).
/// </summary>
public static class DocumentBundleTextKeys
{
    public const string Documents = "share.documents";
    public const string Download = "share.download";
    public const string DownloadAll = "share.downloadAll";
    public const string Print = "share.print";
    public const string DownloadPdf = "share.downloadPdf";
    public const string DownloadAllPdf = "share.downloadAllPdf";
    public const string DownloadZip = "share.downloadZip";
    public const string OpenInNewTab = "share.openInNewTab";
    public const string ExportCsv = "share.exportCsv";
    public const string DownloadJson = "share.downloadJson";
    public const string AllTables = "share.allTables";
    public const string CopyJson = "share.copyJson";
    public const string Copied = "share.copied";
    public const string AvailableUntil = "share.availableUntil";
    public const string SharedBy = "share.sharedBy";
    public const string UnavailableTitle = "share.unavailable.title";
    public const string ExpiredMessage = "share.unavailable.expired";
    public const string RevokedMessage = "share.unavailable.revoked";
    public const string NotFoundMessage = "share.unavailable.notFound";

    /// <summary>
    /// Clave y ayuda para quien traduce, en el orden en que se siembran.
    /// </summary>
    public static IReadOnlyList<(string Key, string Description)> All { get; } =
    [
        (Documents, "Título de la lista de documentos"),
        (Download, "Botón de descarga de un documento (sin PDF)"),
        (DownloadAll, "Botón para descargar todo (sin PDF)"),
        (Print, "Botón de imprimir"),
        (DownloadPdf, "Botón de descarga de un documento en PDF"),
        (DownloadAllPdf, "Botón para descargar todo en un PDF"),
        (DownloadZip, "Botón para descargar todo en ZIP"),
        (OpenInNewTab, "Enlace para abrir el documento en otra pestaña"),
        (ExportCsv, "Menú para exportar las tablas del documento a CSV (bundles con datos)"),
        (DownloadJson, "Botón para descargar los datos del documento en JSON (bundles con datos)"),
        (AllTables, "Opción del menú CSV que exporta todas las tablas"),
        (CopyJson, "Opción del menú JSON que copia los datos al portapapeles"),
        (Copied, "Aviso breve después de copiar los datos"),
        (AvailableUntil, "Caducidad. {0} = fecha; no quites el {0}"),
        (SharedBy, "Remitente. {0} = nombre de la empresa; no quites el {0}"),
        (UnavailableTitle, "Título de la página de enlace no disponible"),
        (ExpiredMessage, "Mensaje de enlace caducado"),
        (RevokedMessage, "Mensaje de enlace desactivado"),
        (NotFoundMessage, "Mensaje de enlace inexistente o incompleto")
    ];
}
