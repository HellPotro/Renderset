using System.Globalization;
using Renderset.Core.Localization;

namespace Renderset.Core.Sharing;

/// <summary>
/// Textos de la página pública.
///
/// Hay una traducción incorporada para los idiomas de las banderas que ya
/// existen en la aplicación (el resto cae a inglés), y encima de ella el
/// tenant puede escribir las suyas en el Diccionario, ámbito
/// <see cref="ReportResourceScope.Sharing"/>, con las claves de
/// <see cref="DocumentBundleTextKeys"/>. Así cualquier idioma del tenant se
/// puede traducir igual que el resto de recursos, y quien no toca nada sigue
/// viendo los textos de siempre.
/// </summary>
public sealed class DocumentBundleTexts
{
    public required string Language { get; init; }

    public required string Documents { get; init; }

    public required string Download { get; init; }

    public required string DownloadAll { get; init; }

    public required string Print { get; init; }

    public required string DownloadPdf { get; init; }

    public required string DownloadAllPdf { get; init; }

    public required string DownloadZip { get; init; }

    public required string OpenInNewTab { get; init; }

    public required string ExportCsv { get; init; }

    public required string DownloadJson { get; init; }

    /// <summary>Última opción del menú CSV: exportar todas las tablas.</summary>
    public required string AllTables { get; init; }

    public required string CopyJson { get; init; }

    /// <summary>Aviso breve después de copiar el JSON.</summary>
    public required string Copied { get; init; }

    /// <summary>Lleva {0} con la fecha.</summary>
    public required string AvailableUntil { get; init; }

    /// <summary>Lleva {0} con el nombre de la empresa.</summary>
    public required string SharedBy { get; init; }

    public required string UnavailableTitle { get; init; }

    public required string ExpiredMessage { get; init; }

    public required string RevokedMessage { get; init; }

    public required string NotFoundMessage { get; init; }

    public string FormatAvailableUntil(
        DateTime expiresAtUtc)
    {
        var culture = SafeCulture(Language);

        return SafeFormat(
            culture,
            AvailableUntil,
            expiresAtUtc.ToString("d", culture));
    }

    public string FormatSharedBy(
        string company) =>
        SafeFormat(
            CultureInfo.InvariantCulture,
            SharedBy,
            company);

    /// <summary>
    /// Traducción incorporada. Acepta la cultura completa (es-ES) o sólo el
    /// idioma (es).
    /// </summary>
    public static DocumentBundleTexts For(
        string? culture)
    {
        return BuiltIn(LanguageOf(culture)) ?? English;
    }

    /// <summary>
    /// Si RenderSet trae textos propios para ese idioma. Los que no, se
    /// siembran vacíos en el Diccionario para traducirlos allí.
    /// </summary>
    public static bool HasBuiltInTexts(
        string? culture) =>
        BuiltIn(LanguageOf(culture)) is not null;

    /// <summary>
    /// Textos incorporados con lo que el tenant haya escrito encima. Las
    /// claves que falten o vengan vacías se quedan con el incorporado.
    /// </summary>
    public static DocumentBundleTexts For(
        string? culture,
        IReadOnlyDictionary<string, string>? overrides)
    {
        var builtIn = For(culture);

        string Pick(string key, string fallback) =>
            overrides is not null &&
            overrides.TryGetValue(key, out var value) &&
            !string.IsNullOrWhiteSpace(value)
                ? value.Trim()
                : fallback;

        return new DocumentBundleTexts
        {
            // El formato de fechas sigue a la cultura pedida aunque los
            // textos incorporados hayan caído a inglés.
            Language = string.IsNullOrWhiteSpace(culture)
                ? builtIn.Language
                : culture.Trim(),
            Documents = Pick(DocumentBundleTextKeys.Documents, builtIn.Documents),
            Download = Pick(DocumentBundleTextKeys.Download, builtIn.Download),
            DownloadAll = Pick(DocumentBundleTextKeys.DownloadAll, builtIn.DownloadAll),
            Print = Pick(DocumentBundleTextKeys.Print, builtIn.Print),
            DownloadPdf = Pick(DocumentBundleTextKeys.DownloadPdf, builtIn.DownloadPdf),
            DownloadAllPdf = Pick(DocumentBundleTextKeys.DownloadAllPdf, builtIn.DownloadAllPdf),
            DownloadZip = Pick(DocumentBundleTextKeys.DownloadZip, builtIn.DownloadZip),
            OpenInNewTab = Pick(DocumentBundleTextKeys.OpenInNewTab, builtIn.OpenInNewTab),
            ExportCsv = Pick(DocumentBundleTextKeys.ExportCsv, builtIn.ExportCsv),
            DownloadJson = Pick(DocumentBundleTextKeys.DownloadJson, builtIn.DownloadJson),
            AllTables = Pick(DocumentBundleTextKeys.AllTables, builtIn.AllTables),
            CopyJson = Pick(DocumentBundleTextKeys.CopyJson, builtIn.CopyJson),
            Copied = Pick(DocumentBundleTextKeys.Copied, builtIn.Copied),
            AvailableUntil = Pick(DocumentBundleTextKeys.AvailableUntil, builtIn.AvailableUntil),
            SharedBy = Pick(DocumentBundleTextKeys.SharedBy, builtIn.SharedBy),
            UnavailableTitle = Pick(DocumentBundleTextKeys.UnavailableTitle, builtIn.UnavailableTitle),
            ExpiredMessage = Pick(DocumentBundleTextKeys.ExpiredMessage, builtIn.ExpiredMessage),
            RevokedMessage = Pick(DocumentBundleTextKeys.RevokedMessage, builtIn.RevokedMessage),
            NotFoundMessage = Pick(DocumentBundleTextKeys.NotFoundMessage, builtIn.NotFoundMessage)
        };
    }

    /// <summary>
    /// Valor incorporado de una clave, para sembrarla en el Diccionario.
    /// </summary>
    public string? Get(
        string key) =>
        key switch
        {
            DocumentBundleTextKeys.Documents => Documents,
            DocumentBundleTextKeys.Download => Download,
            DocumentBundleTextKeys.DownloadAll => DownloadAll,
            DocumentBundleTextKeys.Print => Print,
            DocumentBundleTextKeys.DownloadPdf => DownloadPdf,
            DocumentBundleTextKeys.DownloadAllPdf => DownloadAllPdf,
            DocumentBundleTextKeys.DownloadZip => DownloadZip,
            DocumentBundleTextKeys.OpenInNewTab => OpenInNewTab,
            DocumentBundleTextKeys.ExportCsv => ExportCsv,
            DocumentBundleTextKeys.DownloadJson => DownloadJson,
            DocumentBundleTextKeys.AllTables => AllTables,
            DocumentBundleTextKeys.CopyJson => CopyJson,
            DocumentBundleTextKeys.Copied => Copied,
            DocumentBundleTextKeys.AvailableUntil => AvailableUntil,
            DocumentBundleTextKeys.SharedBy => SharedBy,
            DocumentBundleTextKeys.UnavailableTitle => UnavailableTitle,
            DocumentBundleTextKeys.ExpiredMessage => ExpiredMessage,
            DocumentBundleTextKeys.RevokedMessage => RevokedMessage,
            DocumentBundleTextKeys.NotFoundMessage => NotFoundMessage,
            _ => null
        };

    private static string LanguageOf(
        string? culture) =>
        string.IsNullOrWhiteSpace(culture)
            ? "en"
            : culture.Trim().Split('-', '_')[0].ToLowerInvariant();

    private static DocumentBundleTexts? BuiltIn(
        string language) =>
        language switch
        {
            "es" => Spanish,
            "en" => English,
            "fr" => French,
            "de" => German,
            "it" => Italian,
            "pt" => Portuguese,
            "pl" => Polish,
            _ => null
        };

    /// <summary>
    /// Un texto del tenant con llaves mal puestas ("{fecha}") no puede tirar
    /// la página pública: se enseña tal cual, con el valor detrás.
    /// </summary>
    private static string SafeFormat(
        CultureInfo culture,
        string template,
        string value)
    {
        try
        {
            return string.Format(culture, template, value);
        }
        catch (FormatException)
        {
            return $"{template} {value}";
        }
    }

    private static CultureInfo SafeCulture(
        string language)
    {
        try
        {
            return CultureInfo.GetCultureInfo(language);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.InvariantCulture;
        }
    }

    private static readonly DocumentBundleTexts Spanish = new()
    {
        Language = "es",
        Documents = "Documentos",
        Download = "Descargar",
        DownloadAll = "Descargar todo",
        Print = "Imprimir",
        DownloadPdf = "Descargar PDF",
        DownloadAllPdf = "Todo en PDF",
        DownloadZip = "ZIP",
        OpenInNewTab = "Abrir en una pestaña nueva",
        ExportCsv = "Exportar CSV",
        DownloadJson = "Descargar JSON",
        AllTables = "Todas las tablas",
        CopyJson = "Copiar JSON",
        Copied = "Datos copiados.",
        AvailableUntil = "Disponible hasta el {0}",
        SharedBy = "Compartido por {0}",
        UnavailableTitle = "Enlace no disponible",
        ExpiredMessage = "Este enlace ha caducado. Pide a quien te lo envió que te mande uno nuevo.",
        RevokedMessage = "Este enlace se ha desactivado. Pide a quien te lo envió que te mande uno nuevo.",
        NotFoundMessage = "No encontramos documentos para este enlace. Comprueba que lo has copiado completo."
    };

    private static readonly DocumentBundleTexts English = new()
    {
        Language = "en",
        Documents = "Documents",
        Download = "Download",
        DownloadAll = "Download all",
        Print = "Print",
        DownloadPdf = "Download PDF",
        DownloadAllPdf = "All as PDF",
        DownloadZip = "ZIP",
        OpenInNewTab = "Open in a new tab",
        ExportCsv = "Export CSV",
        DownloadJson = "Download JSON",
        AllTables = "All tables",
        CopyJson = "Copy JSON",
        Copied = "Data copied.",
        AvailableUntil = "Available until {0}",
        SharedBy = "Shared by {0}",
        UnavailableTitle = "Link not available",
        ExpiredMessage = "This link has expired. Please ask the sender for a new one.",
        RevokedMessage = "This link has been deactivated. Please ask the sender for a new one.",
        NotFoundMessage = "We couldn't find any documents for this link. Please check that you copied it in full."
    };

    private static readonly DocumentBundleTexts French = new()
    {
        Language = "fr",
        Documents = "Documents",
        Download = "Télécharger",
        DownloadAll = "Tout télécharger",
        Print = "Imprimer",
        DownloadPdf = "Télécharger le PDF",
        DownloadAllPdf = "Tout en PDF",
        DownloadZip = "ZIP",
        OpenInNewTab = "Ouvrir dans un nouvel onglet",
        ExportCsv = "Exporter en CSV",
        DownloadJson = "Télécharger le JSON",
        AllTables = "Tous les tableaux",
        CopyJson = "Copier le JSON",
        Copied = "Données copiées.",
        AvailableUntil = "Disponible jusqu'au {0}",
        SharedBy = "Partagé par {0}",
        UnavailableTitle = "Lien indisponible",
        ExpiredMessage = "Ce lien a expiré. Demandez-en un nouveau à l'expéditeur.",
        RevokedMessage = "Ce lien a été désactivé. Demandez-en un nouveau à l'expéditeur.",
        NotFoundMessage = "Aucun document trouvé pour ce lien. Vérifiez que vous l'avez copié en entier."
    };

    private static readonly DocumentBundleTexts German = new()
    {
        Language = "de",
        Documents = "Dokumente",
        Download = "Herunterladen",
        DownloadAll = "Alle herunterladen",
        Print = "Drucken",
        DownloadPdf = "PDF herunterladen",
        DownloadAllPdf = "Alles als PDF",
        DownloadZip = "ZIP",
        OpenInNewTab = "In neuem Tab öffnen",
        ExportCsv = "Als CSV exportieren",
        DownloadJson = "JSON herunterladen",
        AllTables = "Alle Tabellen",
        CopyJson = "JSON kopieren",
        Copied = "Daten kopiert.",
        AvailableUntil = "Verfügbar bis {0}",
        SharedBy = "Geteilt von {0}",
        UnavailableTitle = "Link nicht verfügbar",
        ExpiredMessage = "Dieser Link ist abgelaufen. Bitte fordern Sie beim Absender einen neuen an.",
        RevokedMessage = "Dieser Link wurde deaktiviert. Bitte fordern Sie beim Absender einen neuen an.",
        NotFoundMessage = "Zu diesem Link wurden keine Dokumente gefunden. Bitte prüfen Sie, ob er vollständig kopiert wurde."
    };

    private static readonly DocumentBundleTexts Italian = new()
    {
        Language = "it",
        Documents = "Documenti",
        Download = "Scarica",
        DownloadAll = "Scarica tutto",
        Print = "Stampa",
        DownloadPdf = "Scarica PDF",
        DownloadAllPdf = "Tutto in PDF",
        DownloadZip = "ZIP",
        OpenInNewTab = "Apri in una nuova scheda",
        ExportCsv = "Esporta CSV",
        DownloadJson = "Scarica JSON",
        AllTables = "Tutte le tabelle",
        CopyJson = "Copia JSON",
        Copied = "Dati copiati.",
        AvailableUntil = "Disponibile fino al {0}",
        SharedBy = "Condiviso da {0}",
        UnavailableTitle = "Link non disponibile",
        ExpiredMessage = "Questo link è scaduto. Chiedi al mittente di inviartene uno nuovo.",
        RevokedMessage = "Questo link è stato disattivato. Chiedi al mittente di inviartene uno nuovo.",
        NotFoundMessage = "Non abbiamo trovato documenti per questo link. Verifica di averlo copiato per intero."
    };

    private static readonly DocumentBundleTexts Portuguese = new()
    {
        Language = "pt",
        Documents = "Documentos",
        Download = "Descarregar",
        DownloadAll = "Descarregar tudo",
        Print = "Imprimir",
        DownloadPdf = "Descarregar PDF",
        DownloadAllPdf = "Tudo em PDF",
        DownloadZip = "ZIP",
        OpenInNewTab = "Abrir num novo separador",
        ExportCsv = "Exportar CSV",
        DownloadJson = "Descarregar JSON",
        AllTables = "Todas as tabelas",
        CopyJson = "Copiar JSON",
        Copied = "Dados copiados.",
        AvailableUntil = "Disponível até {0}",
        SharedBy = "Partilhado por {0}",
        UnavailableTitle = "Ligação indisponível",
        ExpiredMessage = "Esta ligação expirou. Peça ao remetente que lhe envie uma nova.",
        RevokedMessage = "Esta ligação foi desativada. Peça ao remetente que lhe envie uma nova.",
        NotFoundMessage = "Não encontrámos documentos para esta ligação. Verifique se a copiou completa."
    };

    private static readonly DocumentBundleTexts Polish = new()
    {
        Language = "pl",
        Documents = "Dokumenty",
        Download = "Pobierz",
        DownloadAll = "Pobierz wszystko",
        Print = "Drukuj",
        DownloadPdf = "Pobierz PDF",
        DownloadAllPdf = "Wszystko w PDF",
        DownloadZip = "ZIP",
        OpenInNewTab = "Otwórz w nowej karcie",
        ExportCsv = "Eksportuj CSV",
        DownloadJson = "Pobierz JSON",
        AllTables = "Wszystkie tabele",
        CopyJson = "Kopiuj JSON",
        Copied = "Dane skopiowane.",
        AvailableUntil = "Dostępne do {0}",
        SharedBy = "Udostępnione przez {0}",
        UnavailableTitle = "Link niedostępny",
        ExpiredMessage = "Ten link wygasł. Poproś nadawcę o nowy.",
        RevokedMessage = "Ten link został dezaktywowany. Poproś nadawcę o nowy.",
        NotFoundMessage = "Nie znaleziono dokumentów dla tego linku. Sprawdź, czy został skopiowany w całości."
    };
}
