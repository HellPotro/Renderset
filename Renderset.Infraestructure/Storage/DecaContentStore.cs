using Renderset.Core.Rendering;

namespace Renderset.Infrastructure.Storage;

/// <summary>
/// Almacén de los PDF de DeCA, aparte del de los documentos de RenderSet
/// (Deca:Storage, mismo formato que DocumentStorage). Es un tipo propio y
/// no un IDocumentContentStore registrado para que el contenedor de
/// dependencias no confunda uno con otro.
///
/// Sin configurar, los PDF van en la base de datos Deca (DecaVersions.
/// PdfContent): 5 MB como mucho cada uno, y todo en una transacción.
/// </summary>
public sealed class DecaContentStore(
    IDocumentContentStore inner)
{
    public Task<string> SaveAsync(
        string tenantId,
        string decaId,
        int version,
        string number,
        byte[] pdf,
        CancellationToken cancellationToken = default) =>
        inner.SaveAsync(
            tenantId,
            $"{decaId}-v{version}",
            $"{number}-v{version}.pdf",
            "application/pdf",
            pdf,
            cancellationToken);

    public Task<byte[]?> ReadAsync(
        string path,
        CancellationToken cancellationToken = default) =>
        inner.ReadAsync(
            path,
            cancellationToken);
}
