using Renderset.Core.Rendering;

namespace Renderset.Infrastructure.Storage;

/// <summary>
/// Almacén de las imágenes de los tenants (Assets:Storage, mismo formato
/// que DocumentStorage; contenedor "assets" por defecto). Tipo propio, como
/// DecaContentStore, para que no se confunda con el de los documentos.
///
/// Sin configurar, las imágenes van en la base de datos de RenderSet
/// (TenantAssets.Content): un logo ocupa poco.
/// </summary>
public sealed class TenantAssetContentStore(
    IDocumentContentStore inner)
{
    public Task<string> SaveAsync(
        string tenantId,
        string assetId,
        string fileName,
        string contentType,
        byte[] content,
        CancellationToken cancellationToken = default) =>
        inner.SaveAsync(
            tenantId,
            $"asset-{assetId}",
            fileName,
            contentType,
            content,
            cancellationToken);

    public Task<byte[]?> ReadAsync(
        string path,
        CancellationToken cancellationToken = default) =>
        inner.ReadAsync(
            path,
            cancellationToken);
}
