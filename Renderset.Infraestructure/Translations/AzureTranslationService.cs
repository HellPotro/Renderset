using Microsoft.Extensions.Options;
using Renderset.Core.Translations;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Renderset.Infrastructure.Translations;

public sealed partial class AzureTranslationService : ITranslationService
{
    private const string ProviderName = "AzureTranslator";

    private readonly HttpClient _httpClient;
    private readonly AzureTranslatorOptions _options;

    public AzureTranslationService(
        HttpClient httpClient,
        IOptions<AzureTranslatorOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<TranslationResult>> TranslateAsync(
        TranslationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(_options.Key))
            throw new InvalidOperationException("AzureTranslator:Key no está configurado.");

        if (request.Items.Count == 0)
            return [];

        var sourceLanguage =
            NormalizeLanguage(request.SourceCulture);

        var targetLanguage =
            NormalizeLanguage(request.TargetCulture);

        var protectedItems =
            request.Items
                .Select(item => new ProtectedTranslationItem(
                    item,
                    ProtectPlaceholders(item.Text)))
                .ToList();

        var uri =
            $"/translate?api-version=3.0&from={Uri.EscapeDataString(sourceLanguage)}&to={Uri.EscapeDataString(targetLanguage)}";

        using var httpRequest =
            new HttpRequestMessage(
                HttpMethod.Post,
                uri);

        httpRequest.Headers.Add(
            "Ocp-Apim-Subscription-Key",
            _options.Key);

        if (!string.IsNullOrWhiteSpace(_options.Region))
        {
            httpRequest.Headers.Add(
                "Ocp-Apim-Subscription-Region",
                _options.Region);
        }

        httpRequest.Headers.Add(
            "X-ClientTraceId",
            Guid.NewGuid().ToString());

        httpRequest.Content =
            JsonContent.Create(
                protectedItems.Select(x => new AzureTranslateInput
                {
                    Text = x.ProtectedText.Text
                }));

        using var response =
            await _httpClient.SendAsync(
                httpRequest,
                cancellationToken);

        var content =
            await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Azure Translator devolvió {(int)response.StatusCode}: {content}");
        }

        var azureResults =
            JsonSerializer.Deserialize<List<AzureTranslateResponse>>(content)
            ?? [];

        var results =
            new List<TranslationResult>();

        for (var i = 0; i < protectedItems.Count; i++)
        {
            var source =
                protectedItems[i];

            var translatedText =
                azureResults.ElementAtOrDefault(i)?
                    .Translations
                    .FirstOrDefault()?
                    .Text;

            if (string.IsNullOrWhiteSpace(translatedText))
                continue;

            results.Add(
                new TranslationResult
                {
                    Key = source.Item.Key,
                    SourceText = source.Item.Text,
                    TranslatedText = RestorePlaceholders(
                        translatedText,
                        source.ProtectedText.Placeholders),
                    Provider = ProviderName
                });
        }

        return results;
    }

    private static string NormalizeLanguage(string culture)
    {
        if (string.IsNullOrWhiteSpace(culture))
            throw new ArgumentException(
                "La cultura no puede estar vacía.",
                nameof(culture));

        var trimmed =
            culture.Trim();

        var separatorIndex =
            trimmed.IndexOfAny(['-', '_']);

        return separatorIndex > 0
            ? trimmed[..separatorIndex].ToLowerInvariant()
            : trimmed.ToLowerInvariant();
    }

    private static ProtectedText ProtectPlaceholders(string text)
    {
        if (string.IsNullOrEmpty(text))
            return new ProtectedText(text, new Dictionary<string, string>());

        var placeholders =
            new Dictionary<string, string>();

        var index =
            0;

        var protectedText =
            PlaceholderRegex().Replace(
                text,
                match =>
                {
                    var token =
                        $"[[[RS_PLACEHOLDER_{index++}]]]";

                    placeholders[token] =
                        match.Value;

                    return token;
                });

        return new ProtectedText(
            protectedText,
            placeholders);
    }

    /// <summary>
    /// Devuelve los marcadores a su sitio.
    ///
    /// No se busca el texto exacto del marcador: el traductor lo trata como
    /// una palabra desconocida y a veces le mete espacios o le cambia los
    /// corchetes de sitio. Se localiza por su número, que es lo único que
    /// siempre sobrevive, y así una restauración no falla en silencio
    /// dejando "[[[RS_PLACEHOLDER_0]]]" guardado en el diccionario.
    /// </summary>
    private static string RestorePlaceholders(
        string text,
        IReadOnlyDictionary<string, string> placeholders)
    {
        if (placeholders.Count == 0)
            return text;

        return RestoreRegex().Replace(
            text,
            match =>
            {
                var token =
                    $"[[[RS_PLACEHOLDER_{match.Groups["index"].Value}]]]";

                return placeholders.TryGetValue(token, out var original)
                    ? original
                    : match.Value;
            });
    }

    [GeneratedRegex(@"\{\{\s*[^{}]+?\s*\}\}|\{\d+\}", RegexOptions.Compiled)]
    private static partial Regex PlaceholderRegex();

    [GeneratedRegex(
        @"\[{2,4}\s*RS[\s_]*PLACEHOLDER[\s_]*(?<index>\d+)\s*\]{2,4}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex RestoreRegex();

    private sealed record ProtectedTranslationItem(
        TranslationItem Item,
        ProtectedText ProtectedText);

    private sealed record ProtectedText(
        string Text,
        IReadOnlyDictionary<string, string> Placeholders);

    private sealed class AzureTranslateInput
    {
        [JsonPropertyName("text")]
        public required string Text { get; set; }
    }

    private sealed class AzureTranslateResponse
    {
        [JsonPropertyName("translations")]
        public List<AzureTranslation> Translations { get; set; } = [];
    }

    private sealed class AzureTranslation
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("to")]
        public string? To { get; set; }
    }
}
