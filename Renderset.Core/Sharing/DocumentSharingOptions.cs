namespace Renderset.Core.Sharing;

/// <summary>
/// Sección "DocumentSharing" de la configuración de la API.
/// </summary>
public sealed class DocumentSharingOptions
{
    public const string SectionName = "DocumentSharing";

    /// <summary>
    /// Caducidad cuando la petición no dice nada. Un enlace que no caduca
    /// nunca acaba reenviado a quien no debe años después.
    /// </summary>
    public int DefaultExpirationDays { get; set; } = 30;

    public int MaxExpirationDays { get; set; } = 365;

    public int MaxDocumentsPerBundle { get; set; } = 20;

    /// <summary>
    /// Dominio público con el que se construyen los enlaces que se mandan al
    /// cliente, por ejemplo "https://docs.renderset.io". Sin él se usa el
    /// host de la petición, que detrás de un proxy puede ser una IP interna.
    /// </summary>
    public string? PublicBaseUrl { get; set; }
}
