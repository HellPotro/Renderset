namespace Renderset.Core.Sharing;

/// <summary>
/// Respuesta de los endpoints de gestión de bundles.
///
/// <see cref="Url"/> sólo viene al crear el bundle o renovar su enlace: el
/// token no se guarda en claro, así que después no hay forma de volver a
/// construir el enlace. Quien integra tiene que guardarlo en ese momento
/// (o pedir uno nuevo, que invalida el anterior).
/// </summary>
public sealed class DocumentBundleResponse
{
    public required Guid BundleId { get; init; }

    public string? Url { get; init; }

    public required string Title { get; init; }

    public string? Message { get; init; }

    public required string Culture { get; init; }

    public DocumentBundleStatus Status { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public DateTime ExpiresAtUtc { get; init; }

    public DateTime? RevokedAtUtc { get; init; }

    public int AccessCount { get; init; }

    public DateTime? FirstAccessedAtUtc { get; init; }

    public DateTime? LastAccessedAtUtc { get; init; }

    public IReadOnlyList<DocumentBundleResponseItem> Documents { get; init; } = [];

    public static DocumentBundleResponse From(
        DocumentBundle bundle,
        DateTime nowUtc,
        string? url = null)
    {
        ArgumentNullException.ThrowIfNull(bundle);

        return new DocumentBundleResponse
        {
            BundleId = bundle.Id,
            Url = url,
            Title = bundle.Title,
            Message = bundle.Message,
            Culture = bundle.Culture,
            Status = bundle.GetStatus(nowUtc),
            CreatedAtUtc = bundle.CreatedAtUtc,
            ExpiresAtUtc = bundle.ExpiresAtUtc,
            RevokedAtUtc = bundle.RevokedAtUtc,
            AccessCount = bundle.AccessCount,
            FirstAccessedAtUtc = bundle.FirstAccessedAtUtc,
            LastAccessedAtUtc = bundle.LastAccessedAtUtc,
            Documents = bundle.Items
                .OrderBy(x => x.Position)
                .Select(x => new DocumentBundleResponseItem
                {
                    Position = x.Position,
                    DocumentId = x.DocumentId,
                    DisplayName = x.DisplayName
                })
                .ToList()
        };
    }
}


public sealed class DocumentBundleResponseItem
{
    public required int Position { get; init; }

    public required string DocumentId { get; init; }

    public required string DisplayName { get; init; }
}
