namespace Renderset.Core.Sharing;

/// <summary>
/// Cifra el token del enlace para poder recuperarlo después desde la
/// gestión interna (abrir, copiar), sin guardarlo en claro.
///
/// Interfaz en Core e implementación en la API, que es quien tiene ASP.NET
/// Data Protection y sus claves.
/// </summary>
public interface IBundleTokenProtector
{
    string Protect(
        string token);

    /// <summary>
    /// Nulo si no se puede descifrar: claves rotadas o perdidas, o un valor
    /// manipulado. No es un error de la petición, sólo que ese enlace ya no
    /// se puede recuperar.
    /// </summary>
    string? Unprotect(
        string protectedToken);
}
