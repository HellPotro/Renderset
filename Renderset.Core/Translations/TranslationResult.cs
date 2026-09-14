namespace Renderset.Core.Translations;

public sealed class TranslationResult
{
    public required string Key { get; set; }

    public required string SourceText { get; set; }

    public required string TranslatedText { get; set; }

    public required string Provider { get; set; }
}
