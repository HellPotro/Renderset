namespace Renderset.Infrastructure.Persistence.Entities;

/// <summary>
/// Imagen de un tenant (logo). El contenido va en la fila (Content) o, con
/// Assets:Storage, en su almacén (Path): nunca en los dos.
/// </summary>
public sealed class TenantAssetEntity
{
    public string TenantId { get; set; } = default!;

    public string AssetId { get; set; } = default!;

    public string ContentType { get; set; } = default!;

    public string? FileName { get; set; }

    public long SizeBytes { get; set; }

    public byte[]? Content { get; set; }

    public string? Path { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public string? CreatedBy { get; set; }
}
