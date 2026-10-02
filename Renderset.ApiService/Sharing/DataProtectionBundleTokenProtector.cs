using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Renderset.Core.Sharing;

namespace Renderset.Api.Sharing;

/// <summary>
/// Cifra el token de los enlaces con ASP.NET Data Protection.
///
/// Las claves viven fuera de la base de datos (carpeta o almacén configurado
/// en DocumentSharing:DataProtectionKeysPath). Si se pierden, los enlaces ya
/// enviados siguen funcionando, porque el acceso se valida con el hash; lo
/// único que se pierde es poder volver a copiarlos desde RenderSet.
/// </summary>
public sealed class DataProtectionBundleTokenProtector
    : IBundleTokenProtector
{
    /// <summary>
    /// El propósito aísla estas cargas de cualquier otro uso de Data
    /// Protection en la aplicación. Cambiarlo invalida todo lo cifrado.
    /// </summary>
    private const string Purpose = "Renderset.Sharing.BundleToken.v1";

    private readonly IDataProtector _protector;

    public DataProtectionBundleTokenProtector(
        IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector(Purpose);
    }

    public string Protect(
        string token) =>
        _protector.Protect(token);

    public string? Unprotect(
        string protectedToken)
    {
        try
        {
            return _protector.Unprotect(protectedToken);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}
