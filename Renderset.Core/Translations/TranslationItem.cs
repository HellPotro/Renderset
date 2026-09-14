namespace Renderset.Core.Translations;

public sealed class TranslationItem
{
    public required string Key { get; set; }

    public required string Text { get; set; }

    public string? Description { get; set; }

    public string? Context { get; set; }
}
