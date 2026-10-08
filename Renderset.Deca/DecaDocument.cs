namespace Renderset.Deca;

/// <summary>
/// Un DeCA emitido. Lo legal es el PDF de cada versión (congelado al emitir,
/// con su hash); esto es lo que hace falta alrededor para encontrarlo,
/// servirlo por el QR y saber cuándo deja de estar disponible.
///
/// Vive en su propia base de datos (Deca), sin nada que dependa de
/// RenderSet: el PDF de cada versión se guarda con ella.
/// </summary>
public sealed class DecaDocument
{
    /// <summary>
    /// Identificador interno (32 hex). No sale en el QR.
    /// </summary>
    public required string Id { get; init; }

    public required string TenantId { get; init; }

    /// <summary>
    /// Número legible, correlativo por tenant y año: DECA-2026-000123.
    /// </summary>
    public required string Number { get; init; }

    /// <summary>
    /// Código del enlace público del QR (aleatorio, 22 caracteres). Es la
    /// única "credencial" de la descarga: el inspector no tiene cuenta.
    /// </summary>
    public required string PublicCode { get; init; }

    public DecaStatus Status { get; set; } = DecaStatus.Issued;

    /// <summary>
    /// Contenido de la versión vigente.
    /// </summary>
    public required DecaData Data { get; set; }

    /// <summary>
    /// Versión vigente: su PDF es lo que descarga el QR.
    /// </summary>
    public int CurrentVersion { get; set; } = 1;

    /// <summary>
    /// Fecha y hora de emisión: la que llevan los metadatos del PDF.
    /// </summary>
    public DateTime IssuedAtUtc { get; init; }

    public string? IssuedBy { get; init; }

    /// <summary>
    /// Fin del servicio. A partir de aquí empieza a contar el plazo tras el
    /// que se puede desactivar el enlace público.
    /// </summary>
    public DateTime? ServiceEndedAtUtc { get; set; }

    /// <summary>
    /// Desde cuándo el QR deja de descargar. Nulo mientras el servicio no
    /// haya terminado.
    /// </summary>
    public DateTime? PublicUntilUtc { get; set; }

    /// <summary>
    /// Hasta cuándo se conserva como mínimo (un año desde la emisión).
    /// </summary>
    public DateTime RetainUntilUtc { get; init; }

    public List<DecaVersion> Versions { get; set; } = [];

    public List<DecaEvent> Events { get; set; } = [];
}

public enum DecaStatus
{
    /// <summary>
    /// Emitido. El QR descarga el PDF.
    /// </summary>
    Issued,

    /// <summary>
    /// Servicio terminado. El QR sigue descargando hasta
    /// <see cref="DecaDocument.PublicUntilUtc"/>.
    /// </summary>
    Finished
}

/// <summary>
/// Una versión del PDF. La primera es la emisión; las siguientes, las
/// modificaciones (siguiente fase).
/// </summary>
public sealed class DecaVersion
{
    public int Version { get; init; }

    public required DecaData Data { get; init; }

    /// <summary>
    /// SHA-256 del PDF tal como se guardó: prueba de que lo que se descarga
    /// es lo que se emitió.
    /// </summary>
    public required string Sha256 { get; init; }

    public long SizeBytes { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public string? CreatedBy { get; init; }

    /// <summary>
    /// Motivo de la modificación. Nulo en la emisión.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// Versión del diseño del tenant con la que se pintó (0 = el base).
    /// </summary>
    public int TemplateVersion { get; init; }
}

/// <summary>
/// Registro de lo que ha pasado con el DeCA. Sólo se añade, nunca se
/// modifica: es la trazabilidad que pide la norma.
/// </summary>
public sealed class DecaEvent
{
    public required DecaEventKind Kind { get; init; }

    public DateTime AtUtc { get; init; }

    public string? Actor { get; init; }

    public string? Detail { get; init; }
}

public enum DecaEventKind
{
    Issued,
    ServiceFinished,
    PublicDownloadDisabled
}

/// <summary>
/// Fila del listado, sin contenido.
/// </summary>
public sealed class DecaSummary
{
    public required string Id { get; init; }

    public required string Number { get; init; }

    public DecaStatus Status { get; init; }

    public string? Reference { get; init; }

    public string? ShipperName { get; init; }

    public string? CarrierName { get; init; }

    public string? TractorPlate { get; init; }

    public DateOnly? TransportDate { get; init; }

    public int ShipmentCount { get; init; }

    public int CurrentVersion { get; init; }

    public DateTime IssuedAtUtc { get; init; }

    public DateTime? PublicUntilUtc { get; init; }
}

public sealed class DecaQuery
{
    /// <summary>
    /// Número, referencia, cargador, transportista o matrícula.
    /// </summary>
    public string? Search { get; init; }

    public DateOnly? From { get; init; }

    public DateOnly? To { get; init; }

    public int Take { get; init; } = 50;

    public int Skip { get; init; }
}
