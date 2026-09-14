namespace Renderset.Core.Translations;

public interface ITranslationService
{
    Task<IReadOnlyList<TranslationResult>> TranslateAsync(
        TranslationRequest request,
        CancellationToken cancellationToken = default);
}
