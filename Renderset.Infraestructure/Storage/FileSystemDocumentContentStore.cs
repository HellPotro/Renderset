using Renderset.Core.Rendering;

namespace Renderset.Infrastructure.Storage;

/// <summary>
/// Contenido de documentos en una carpeta. Pensado para desarrollo y para
/// instalaciones en servidor propio sin Azure.
/// </summary>
public sealed class FileSystemDocumentContentStore
    : IDocumentContentStore
{
    private readonly string _root;

    public FileSystemDocumentContentStore(
        string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw new InvalidOperationException(
                "DocumentStorage:RootPath es obligatorio con el proveedor FileSystem.");
        }

        _root =
            Path.GetFullPath(rootPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(
        string tenantId,
        string documentId,
        string fileName,
        string contentType,
        byte[] content,
        CancellationToken cancellationToken = default)
    {
        var path =
            DocumentStorageOptions.BuildPath(
                tenantId,
                documentId,
                fileName,
                DateTime.UtcNow);

        var fullPath = Resolve(path);

        Directory.CreateDirectory(
            Path.GetDirectoryName(fullPath)!);

        await File.WriteAllBytesAsync(
            fullPath,
            content,
            cancellationToken);

        return path;
    }

    public async Task<byte[]?> ReadAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        var fullPath = Resolve(path);

        return File.Exists(fullPath)
            ? await File.ReadAllBytesAsync(fullPath, cancellationToken)
            : null;
    }

    public Task DeleteAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        var fullPath = Resolve(path);

        if (File.Exists(fullPath))
            File.Delete(fullPath);

        return Task.CompletedTask;
    }

    /// <summary>
    /// La ruta viene de la base de datos, pero se comprueba igual que no se
    /// sale de la raíz: una fila manipulada no puede servir para leer
    /// cualquier fichero del servidor.
    /// </summary>
    private string Resolve(
        string path)
    {
        var fullPath =
            Path.GetFullPath(
                Path.Combine(
                    _root,
                    path.Replace('/', Path.DirectorySeparatorChar)));

        if (!fullPath.StartsWith(_root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"La ruta '{path}' queda fuera de la carpeta de documentos.");
        }

        return fullPath;
    }
}
