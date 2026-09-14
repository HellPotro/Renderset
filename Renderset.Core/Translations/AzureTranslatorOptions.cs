namespace Renderset.Core.Translations;

public sealed class AzureTranslatorOptions
{
    public const string SectionName = "AzureTranslator";

    /// <summary>
    /// Ejemplo: https://api.cognitive.microsofttranslator.com
    /// </summary>
    public string Endpoint { get; set; } = "https://api.cognitive.microsofttranslator.com";

    public string? Key { get; set; }

    /// <summary>
    /// Región del recurso, por ejemplo westeurope. Necesaria normalmente con endpoint global.
    /// </summary>
    public string? Region { get; set; }
}
