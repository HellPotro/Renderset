namespace Renderset.Deca;

/// <summary>
/// DeCA emitidos. No hay borrado: un DeCA se conserva como mínimo un año, y
/// la forma más simple de no incumplirlo es que no exista la operación.
/// </summary>
public interface IDecaRepository
{
    /// <summary>
    /// Siguiente número del año para el tenant (1, 2, 3...). Atómico: dos
    /// emisiones a la vez nunca reciben el mismo. Un número reservado cuya
    /// emisión falla se pierde, y es correcto: el DeCA no exige numeración
    /// sin huecos.
    /// </summary>
    Task<int> NextSequenceAsync(
        string tenantId,
        int year,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Guarda el DeCA con su primera versión y el PDF de esa versión. Con
    /// el PDF en la base de datos va todo en una transacción; con almacén
    /// externo, el fichero se sube antes que la fila, así que nunca hay un
    /// DeCA cuyo QR no tenga PDF.
    /// </summary>
    Task AddAsync(
        DecaDocument deca,
        byte[] pdf,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// PDF de una versión (la vigente si es nula), tal como se guardó.
    /// </summary>
    Task<byte[]?> GetPdfAsync(
        string tenantId,
        string decaId,
        int? version = null,
        CancellationToken cancellationToken = default);

    Task<DecaDocument?> GetAsync(
        string tenantId,
        string decaId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Para el QR: llega sin tenant, sólo con el código. Sin versiones ni
    /// eventos.
    /// </summary>
    Task<DecaPublicTarget?> FindByPublicCodeAsync(
        string publicCode,
        CancellationToken cancellationToken = default);

    Task<DecaPage> SearchAsync(
        string tenantId,
        DecaQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fin del servicio: estado, plazo público y el evento, juntos.
    /// </summary>
    Task<bool> FinishServiceAsync(
        string tenantId,
        string decaId,
        DateTime endedAtUtc,
        DateTime publicUntilUtc,
        string? actor,
        CancellationToken cancellationToken = default);

    Task AddEventAsync(
        string tenantId,
        string decaId,
        DecaEvent decaEvent,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Lo mínimo para servir el QR.
/// </summary>
public sealed record DecaPublicTarget(
    string TenantId,
    string DecaId,
    string Number,
    int CurrentVersion,
    DateTime? PublicUntilUtc,
    string Sha256);

public sealed class DecaPage
{
    public List<DecaSummary> Items { get; init; } = [];

    public int Total { get; init; }
}
