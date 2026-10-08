namespace Renderset.Blazor.Components.Editor;

/// <summary>
/// Lo que un diseñador no deja quitar. Por defecto nada: el ReportEditor de
/// RenderSet deja ocultar cualquier cosa. Un producto con un documento
/// reglado (el DeCA) pasa aquí sus obligatorios y el editor, en vez de
/// ocultarlos, avisa con un toast de que no se puede.
///
/// Es sólo la interfaz: quien guarda tiene que comprobarlo otra vez (un JSON
/// importado o una llamada a la API no pasan por aquí).
/// </summary>
public sealed class ReportEditorLocks
{
    public static ReportEditorLocks None { get; } = new();

    /// <summary>
    /// Secciones que no se pueden ocultar: id → nombre para el aviso.
    /// </summary>
    public IReadOnlyDictionary<string, string> Sections { get; init; } =
        Empty();

    /// <summary>
    /// Campos que no se pueden ocultar: id → nombre para el aviso.
    /// </summary>
    public IReadOnlyDictionary<string, string> Fields { get; init; } =
        Empty();

    /// <summary>
    /// Columnas de tabla que no se pueden ocultar: id → nombre.
    /// </summary>
    public IReadOnlyDictionary<string, string> Columns { get; init; } =
        Empty();

    /// <summary>
    /// La cabecera no se puede apagar.
    /// </summary>
    public bool Header { get; init; }

    /// <summary>
    /// Contenido fijo del QR de la cabecera ({pdfUrl}...). Con valor, el QR
    /// es obligatorio y no se puede cambiar lo que lleva.
    /// </summary>
    public string? QrContent { get; init; }

    /// <summary>
    /// Cómo se llama el QR fijo en la barra ("Descarga del PDF").
    /// </summary>
    public string QrDescription { get; init; } = "Contenido fijo";

    /// <summary>
    /// Tamaño mínimo del QR en px.
    /// </summary>
    public int? MinQrSize { get; init; }

    /// <summary>
    /// Por qué no se puede: va detrás del nombre en el aviso.
    /// </summary>
    public string Reason { get; init; } =
        "es obligatorio y no se puede quitar.";

    public bool IsEmpty =>
        Sections.Count == 0 &&
        Fields.Count == 0 &&
        Columns.Count == 0 &&
        !Header &&
        QrContent is null;

    public string? Section(string? id) =>
        Find(Sections, id);

    public string? Field(string? id) =>
        Find(Fields, id);

    public string? Column(string? id) =>
        Find(Columns, id);

    private static string? Find(
        IReadOnlyDictionary<string, string> items,
        string? id) =>
        !string.IsNullOrWhiteSpace(id) && items.TryGetValue(id, out var name)
            ? name
            : null;

    public static IReadOnlyDictionary<string, string> Map(
        IEnumerable<(string Id, string Name)> items) =>
        items
            .GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                x => x.Key,
                x => x.First().Name,
                StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyDictionary<string, string> Empty() =>
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Lo que el ReportEditor pasa en cascada a sus piezas (secciones, filas,
/// cabecera): los bloqueos vigentes y cómo avisar al chocar con uno.
/// </summary>
public sealed class ReportEditorLockContext
{
    private readonly Func<ReportEditorLocks> _locks;
    private readonly Action<string> _rejected;

    public ReportEditorLockContext(
        Func<ReportEditorLocks> locks,
        Action<string> rejected)
    {
        _locks = locks;
        _rejected = rejected;
    }

    public ReportEditorLocks Locks =>
        _locks() ?? ReportEditorLocks.None;

    /// <summary>
    /// Avisa de que <paramref name="name"/> no se puede quitar.
    /// </summary>
    public void Reject(
        string name) =>
        _rejected($"{name}: {Locks.Reason}");
}
