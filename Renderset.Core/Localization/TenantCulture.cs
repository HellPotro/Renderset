namespace Renderset.Core.Localization;

public sealed class TenantCulture
{
    /// <summary>
    /// Cultura en formato BCP-47 (es-ES, en-GB). Se guarda completa porque
    /// hace falta para formatear fechas, números y divisa, no sólo para
    /// buscar en el diccionario.
    /// </summary>
    public required string Culture { get; set; }

    public required string DisplayName { get; set; }

    public bool IsDefault { get; set; }
}
