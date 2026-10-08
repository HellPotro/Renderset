using Azure;
using Azure.Identity;
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

    private AzureBlobDocumentContentStore(
        BlobContainerClient container)
    {
        _container = container;
    }

    /// <summary>
    /// Elige la forma de autenticarse según lo configurado:
    ///
    /// - ConnectionString: con la clave de la cuenta. Cómodo en desarrollo
    ///   (user-secrets), pero la clave da acceso total a la cuenta.
    /// - AccountUrl sin ConnectionString: identidad administrada en Azure
    ///   (o tu usuario de Visual Studio / az login en local), sin ninguna
    ///   clave en la configuración. Es lo recomendado en App Service; la
    ///   identidad necesita el rol "Storage Blob Data Contributor".
    /// </summary>
    public static AzureBlobDocumentContentStore Create(
        DocumentStorageOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var containerName =
            string.IsNullOrWhiteSpace(options.ContainerName)
                ? "documents"
                : options.ContainerName.Trim();

        if (!string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            try
            {
                return new AzureBlobDocumentContentStore(
                    new BlobContainerClient(
                        options.ConnectionString.Trim().Trim('"'),
                        containerName));
            }
            catch (FormatException ex)
            {
                // El mensaje del SDK no dice qué valor mirar, y el valor no
                // se puede enseñar porque lleva la clave de la cuenta.
                throw new InvalidOperationException(
                    "DocumentStorage:ConnectionString no tiene formato de cadena de " +
                    "conexión de Azure Storage. Tiene que ser una sola línea con la forma " +
                    "\"DefaultEndpointsProtocol=https;AccountName=...;AccountKey=...;" +
                    "EndpointSuffix=core.windows.net\" (Storage account → Access keys → " +
                    "Connection string). Revisa los user-secrets de Renderset.ApiService.",
                    ex);
            }
        }

        if (!string.IsNullOrWhiteSpace(options.AccountUrl))
        {
            var containerUri =
                new Uri(
                    options.AccountUrl.TrimEnd('/') + "/" + containerName);

            return new AzureBlobDocumentContentStore(
                new BlobContainerClient(
                    containerUri,
                    new DefaultAzureCredential()));
        }

        throw new InvalidOperationException(
            "Con DocumentStorage:Provider = AzureBlob hace falta " +
            "DocumentStorage:ConnectionString o DocumentStorage:AccountUrl " +
            "(identidad administrada).");
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
