using Azure.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Claves de cifrado de ASP.NET Core (cookies de sesión de Web, enlaces de
/// los bundles en la API) que sobreviven a un redespliegue y se comparten
/// entre instancias.
///
/// Sin configurar, ASP.NET las guarda en el disco de la instancia: en App
/// Service se pierden al redesplegar o escalar, y con ellas las sesiones de
/// Web y la posibilidad de volver a abrir los enlaces de los bundles.
///
/// Dos formas, por orden de preferencia:
///
/// 1. Identidad administrada (sin claves en la configuración):
///
///     "DataProtection": {
///       "BlobUri":       "https://&lt;cuenta&gt;.blob.core.windows.net/dataprotection/web-keys.xml",
///       "KeyVaultKeyId": "https://&lt;vault&gt;.vault.azure.net/keys/dataprotection/&lt;versión&gt;"
///     }
///
///    La app necesita "Storage Blob Data Contributor" en el contenedor y
///    "Key Vault Crypto Service Encryption User" en la clave.
///
/// 2. Cadena de conexión del storage (cuando no se pueden asignar roles):
///
///     "DataProtection": {
///       "BlobConnectionString": "DefaultEndpointsProtocol=https;AccountName=…;AccountKey=…",
///       "ContainerName":        "dataprotection",
///       "BlobName":             "web-keys.xml"
///     }
///
///    Las claves quedan en el blob sin cifrar con Key Vault: el contenedor
///    tiene que ser privado y la cadena de conexión, secreta.
///
/// Cada app con su propio blob. El contenedor tiene que existir.
/// </summary>
public static class DataProtectionExtensions
{
    public static IDataProtectionBuilder AddRendersetDataProtection(
        this IServiceCollection services,
        IConfiguration configuration,
        string applicationName,
        string? fallbackKeysPath = null)
    {
        var protection =
            services
                .AddDataProtection()
                .SetApplicationName(applicationName);

        var blobUri = configuration["DataProtection:BlobUri"];
        var keyId = configuration["DataProtection:KeyVaultKeyId"];
        var connectionString = configuration["DataProtection:BlobConnectionString"];

        if (!string.IsNullOrWhiteSpace(blobUri))
        {
            var credential = new DefaultAzureCredential();

            protection.PersistKeysToAzureBlobStorage(new Uri(blobUri), credential);

            if (!string.IsNullOrWhiteSpace(keyId))
                protection.ProtectKeysWithAzureKeyVault(new Uri(keyId), credential);
        }
        else if (!string.IsNullOrWhiteSpace(connectionString))
        {
            var containerName = configuration["DataProtection:ContainerName"];
            var blobName = configuration["DataProtection:BlobName"];

            protection.PersistKeysToAzureBlobStorage(
                connectionString,
                string.IsNullOrWhiteSpace(containerName) ? "dataprotection" : containerName,
                string.IsNullOrWhiteSpace(blobName) ? $"{applicationName.ToLowerInvariant()}-keys.xml" : blobName);
        }
        else if (!string.IsNullOrWhiteSpace(fallbackKeysPath))
        {
            protection.PersistKeysToFileSystem(new DirectoryInfo(fallbackKeysPath));
        }

        return protection;
    }
}
