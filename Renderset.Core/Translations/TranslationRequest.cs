namespace Renderset.Core.Translations;

public sealed class TranslationRequest
{
    public required string SourceCulture { get; set; }

    public required string TargetCulture { get; set; }

    public required IReadOnlyList<TranslationItem> Items { get; set; }
}
