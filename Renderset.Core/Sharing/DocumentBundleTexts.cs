using System.Globalization;

namespace Renderset.Core.Sharing;

/// <summary>
/// Textos fijos de la página pública.
///
/// No salen del diccionario del tenant porque son de RenderSet, no del
/// documento: el cliente de un tenant que sólo tiene español configurado
/// puede ser francés, y los botones de la página tienen que entenderse igual.
/// Son los idiomas de las banderas que ya existen en la aplicación; el resto
/// cae a inglés.
/// </summary>
public sealed class DocumentBundleTexts
{
    public required string Language { get; init; }

    public required string Documents { get; init; }

    public required string Download { get; init; }

    public required string DownloadAll { get; init; }

    public required string Print { get; init; }

    public required string OpenInNewTab { get; init; }

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

        return string.Format(
            culture,
            AvailableUntil,
            expiresAtUtc.ToString("d", culture));
    }

    public string FormatSharedBy(
        string company) =>
        string.Format(
            CultureInfo.InvariantCulture,
            SharedBy,
            company);

    public static DocumentBundleTexts For(
        string? culture)
    {
        var language =
            string.IsNullOrWhiteSpace(culture)
                ? "en"
                : culture.Split('-', '_')[0].ToLowerInvariant();

        return language switch
        {
            "es" => Spanish,
            "fr" => French,
            "de" => German,
            "it" => Italian,
            "pt" => Portuguese,
            "pl" => Polish,
            _ => English
        };
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
        OpenInNewTab = "Abrir en una pestaña nueva",
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
        OpenInNewTab = "Open in a new tab",
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
        OpenInNewTab = "Ouvrir dans un nouvel onglet",
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
        OpenInNewTab = "In neuem Tab öffnen",
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
        OpenInNewTab = "Apri in una nuova scheda",
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
        OpenInNewTab = "Abrir num novo separador",
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
        OpenInNewTab = "Otwórz w nowej karcie",
        AvailableUntil = "Dostępne do {0}",
        SharedBy = "Udostępnione przez {0}",
        UnavailableTitle = "Link niedostępny",
        ExpiredMessage = "Ten link wygasł. Poproś nadawcę o nowy.",
        RevokedMessage = "Ten link został dezaktywowany. Poproś nadawcę o nowy.",
        NotFoundMessage = "Nie znaleziono dokumentów dla tego linku. Sprawdź, czy został skopiowany w całości."
    };
}
