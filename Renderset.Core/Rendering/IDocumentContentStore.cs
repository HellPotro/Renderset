namespace Renderset.Core.Rendering;

/// <summary>
/// Dónde vive el contenido de un documento emitido cuando no va en la base
/// de datos: un contenedor de Azure Blob o una carpeta.
///
/// Es opcional a propósito. Sin ningún almacén registrado, el repositorio
/// sigue guardando el contenido en la columna Content como hasta ahora, así
/// que activarlo es un cambio de configuración y no de código. Y los
/// documentos que ya estaban en la columna se siguen leyendo de ahí.
///
/// Hoy guarda el HTML; cuando exista el motor de PDF, el PDF irá aquí con el
/// mismo contrato.
/// </summary>
public interface IDocumentContentStore
{
    /// <summary>
    /// Guarda el contenido y devuelve la ruta con la que volver a leerlo.
    /// La ruta es opaca para quien llama: se guarda tal cual en la fila.
    /// </summary>
    Task<string> SaveAsync(
        string tenantId,
        string documentId,
        string fileName,
        string contentType,
        byte[] content,
        CancellationToken cancellationToken = default);

    Task<byte[]?> ReadAsync(
        string path,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string path,
        CancellationToken cancellationToken = default);
}
