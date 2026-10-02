using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Renderset.Core.Rendering;

namespace Renderset.Infrastructure.Storage;

/// <summary>
/// Contenido de documentos en un contenedor de Azure Blob Storage.
///
/// El contenedor es privado: los documentos no se sirven nunca desde el blob
/// directamente, siempre a través de la API, que es quien comprueba el token
/// del enlace y registra el acceso.
/// </summary>
public sealed class AzureBlobDocumentContentStore
    : IDocumentContentStore
{
    private readonly BlobContainerClient _container;
    private readonly SemaphoreSlim _ensureLock = new(1, 1);
    private volatile bool _containerReady;

    public AzureBlobDocumentContentStore(
        string connectionString,
        string containerName)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "DocumentStorage:ConnectionString es obligatorio con el proveedor AzureBlob.");
        }

        _container =
            new BlobContainerClient(
                connectionString,
                string.IsNullOrWhiteSpace(containerName)
                    ? "documents"
                    : containerName);
    }

    public async Task<string> SaveAsync(
        string tenantId,
        string documentId,
        string fileName,
        string contentType,
        byte[] content,
        CancellationToken cancellationToken = default)
    {
        await EnsureContainerAsync(cancellationToken);

        var path =
            DocumentStorageOptions.BuildPath(
                tenantId,
                documentId,
                fileName,
                DateTime.UtcNow);

        var blob = _container.GetBlobClient(path);

        await blob.UploadAsync(
            BinaryData.FromBytes(content),
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = contentType
                }
            },
            cancellationToken);

        return path;
    }

    public async Task<byte[]?> ReadAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response =
                await _container
                    .GetBlobClient(path)
                    .DownloadContentAsync(cancellationToken);

            return response.Value.Content.ToArray();
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task DeleteAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        await _container
            .GetBlobClient(path)
            .DeleteIfExistsAsync(
                cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Se crea al primer uso y no al arrancar, para que una cuenta de
    /// almacenamiento caída no impida levantar la API entera.
    /// </summary>
    private async Task EnsureContainerAsync(
        CancellationToken cancellationToken)
    {
        if (_containerReady)
            return;

        await _ensureLock.WaitAsync(cancellationToken);

        try
        {
            if (_containerReady)
                return;

            await _container.CreateIfNotExistsAsync(
                PublicAccessType.None,
                cancellationToken: cancellationToken);

            _containerReady = true;
        }
        finally
        {
            _ensureLock.Release();
        }
    }
}
