// Namespace propio: estas entidades son de DecaDbContext (base de datos
// Deca), no de RenderSetDbContext, que excluye este namespace al cargar sus
// configuraciones.
namespace Renderset.Infrastructure.Persistence.Deca;

/// <summary>
/// DeCA emitido. Las columnas de partes, fecha y matrícula están repetidas
/// fuera del JSON para poder buscar y listar sin abrirlo; el contenido
/// completo es DataJson (el de la versión vigente).
///
/// TenantId es el id del tenant de RenderSet, sin clave ajena: está en
/// otra base de datos.
/// </summary>
public sealed class DecaDocumentEntity
{
    public string DecaId { get; set; } = default!;

    public string TenantId { get; set; } = default!;

    public int Year { get; set; }

    public int Sequence { get; set; }

    public string Number { get; set; } = default!;

    public string PublicCode { get; set; } = default!;

    public string Status { get; set; } = default!;

    public string? Reference { get; set; }

    public string? ShipperName { get; set; }

    public string? ShipperTaxId { get; set; }

    public string? CarrierName { get; set; }

    public string? CarrierTaxId { get; set; }

    public string? TractorPlate { get; set; }

    public DateOnly? TransportDate { get; set; }

    public int ShipmentCount { get; set; }

    public int CurrentVersion { get; set; }

    public string DataJson { get; set; } = default!;

    public DateTime IssuedAtUtc { get; set; }

    public string? IssuedBy { get; set; }

    public DateTime? ServiceEndedAtUtc { get; set; }

    public DateTime? PublicUntilUtc { get; set; }

    public DateTime RetainUntilUtc { get; set; }

    public byte[] RowVersion { get; set; } = default!;

    public ICollection<DecaVersionEntity> Versions { get; set; } = [];

    public ICollection<DecaEventEntity> Events { get; set; } = [];
}


/// <summary>
/// Una versión con su PDF: en PdfContent (base de datos) o en PdfPath
/// (almacén externo, Deca:Storage). Uno de los dos va siempre informado.
/// </summary>
public sealed class DecaVersionEntity
{
    public string DecaId { get; set; } = default!;

    public int Version { get; set; }

    /// <summary>
    /// Repetido para la FK compuesta (DecaId, TenantId): la base de datos
    /// impide una versión con un tenant distinto del de su DeCA.
    /// </summary>
    public string TenantId { get; set; } = default!;

    public string DataJson { get; set; } = default!;

    public string Sha256 { get; set; } = default!;

    public long SizeBytes { get; set; }

    public byte[]? PdfContent { get; set; }

    public string? PdfPath { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public string? CreatedBy { get; set; }

    public string? Reason { get; set; }

    /// <summary>
    /// Versión del diseño del tenant con la que se pintó (0 = el base).
    /// </summary>
    public int TemplateVersion { get; set; }

    public DecaDocumentEntity Deca { get; set; } = default!;
}


/// <summary>
/// Trazabilidad. Sólo se insertan filas.
/// </summary>
public sealed class DecaEventEntity
{
    public long EventId { get; set; }

    public string DecaId { get; set; } = default!;

    public string Kind { get; set; } = default!;

    public DateTime AtUtc { get; set; }

    public string? Actor { get; set; }

    public string? Detail { get; set; }

    public DecaDocumentEntity Deca { get; set; } = default!;
}


/// <summary>
/// Una versión del diseño del DeCA de un tenant. Sólo se insertan: la
/// vigente es la de mayor Version. ConfigurationJson nulo = se restableció
/// el diseño base.
/// </summary>
public sealed class DecaTemplateEntity
{
    public string TenantId { get; set; } = default!;

    public int Version { get; set; }

    public string? ConfigurationJson { get; set; }

    public string? ThemeJson { get; set; }

    public string TextsJson { get; set; } = "{}";

    public DateTime UpdatedAtUtc { get; set; }

    public string? UpdatedBy { get; set; }
}


/// <summary>
/// Último número usado por tenant y año.
/// </summary>
public sealed class DecaSequenceEntity
{
    public string TenantId { get; set; } = default!;

    public int Year { get; set; }

    public int LastNumber { get; set; }
}
