using System.Data;
using System.Data.Common;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Renderset.Deca;
using Renderset.Infrastructure.Persistence;
using Renderset.Infrastructure.Persistence.Deca;
using Renderset.Infrastructure.Storage;

namespace Renderset.Infrastructure.Repositories;

/// <summary>
/// DeCA en su propia base de datos (DecaDbContext). El PDF de cada versión
/// va en la fila (PdfContent) o, con Deca:Storage, en su almacén (PdfPath).
///
/// No hay borrado ni actualización del contenido de una versión: lo
/// emitido no se toca.
/// </summary>
public sealed class EfDecaRepository
    : IDecaRepository
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

    private const int MaxTake = 200;

    private readonly IDbContextFactory<DecaDbContext> _contextFactory;
    private readonly DecaContentStore? _store;

    /// <param name="store">
    /// Opcional: el contenedor pasa null si no hay Deca:Storage, y entonces
    /// el PDF va en la base de datos.
    /// </param>
    public EfDecaRepository(
        IDbContextFactory<DecaDbContext> contextFactory,
        DecaContentStore? store = null)
    {
        _contextFactory = contextFactory;
        _store = store;
    }

    /// <summary>
    /// Un UPDATE con bloqueo de rango (UPDLOCK, HOLDLOCK) dentro de una
    /// transacción: si otra emisión del mismo tenant llega a la vez, espera
    /// a que esta confirme y lee el número siguiente. Si es el primero del
    /// año no hay fila; el bloqueo de rango impide que dos la creen a la vez.
    /// </summary>
    public async Task<int> NextSequenceAsync(
        string tenantId,
        int year,
        CancellationToken cancellationToken = default)
    {
        await using var context =
            await _contextFactory.CreateDbContextAsync(cancellationToken);

        await context.Database.OpenConnectionAsync(cancellationToken);

        var connection = context.Database.GetDbConnection();

        await using var command = connection.CreateCommand();

        command.CommandText =
            """
            SET NOCOUNT ON;
            SET XACT_ABORT ON;

            DECLARE @next int;

            BEGIN TRANSACTION;

            UPDATE dbo.DecaSequences WITH (UPDLOCK, HOLDLOCK)
            SET @next = LastNumber = LastNumber + 1
            WHERE TenantId = @tenantId AND [Year] = @year;

            IF @@ROWCOUNT = 0
            BEGIN
                SET @next = 1;

                INSERT INTO dbo.DecaSequences (TenantId, [Year], LastNumber)
                VALUES (@tenantId, @year, 1);
            END

            COMMIT TRANSACTION;

            SELECT @next;
            """;

        Add(command, "@tenantId", DbType.String, tenantId);
        Add(command, "@year", DbType.Int32, year);

        var result = await command.ExecuteScalarAsync(cancellationToken);

        return Convert.ToInt32(result, System.Globalization.CultureInfo.InvariantCulture);
    }

    public async Task AddAsync(
        DecaDocument deca,
        byte[] pdf,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(deca);
        ArgumentNullException.ThrowIfNull(pdf);

        if (deca.Versions.Count != 1)
            throw new ArgumentException("Un DeCA nuevo lleva exactamente una versión.", nameof(deca));

        // Con almacén externo el fichero se sube antes que la fila: si la
        // fila falla queda un fichero suelto, nunca un QR sin PDF.
        var pdfPath =
            _store is null
                ? null
                : await _store.SaveAsync(
                    deca.TenantId,
                    deca.Id,
                    deca.Versions[0].Version,
                    deca.Number,
                    pdf,
                    cancellationToken);

        await using var context =
            await _contextFactory.CreateDbContextAsync(cancellationToken);

        var (year, sequence) = ParseNumber(deca.Number);

        var entity =
            new DecaDocumentEntity
            {
                DecaId = deca.Id,
                TenantId = deca.TenantId,
                Year = year,
                Sequence = sequence,
                Number = deca.Number,
                PublicCode = deca.PublicCode,
                Status = deca.Status.ToString(),
                CurrentVersion = deca.CurrentVersion,
                DataJson = Serialize(deca.Data),
                IssuedAtUtc = deca.IssuedAtUtc,
                IssuedBy = deca.IssuedBy,
                ServiceEndedAtUtc = deca.ServiceEndedAtUtc,
                PublicUntilUtc = deca.PublicUntilUtc,
                RetainUntilUtc = deca.RetainUntilUtc
            };

        CopySearchColumns(entity, deca.Data);

        foreach (var version in deca.Versions)
        {
            entity.Versions.Add(
                new DecaVersionEntity
                {
                    DecaId = deca.Id,
                    Version = version.Version,
                    TenantId = deca.TenantId,
                    DataJson = Serialize(version.Data),
                    Sha256 = version.Sha256,
                    SizeBytes = version.SizeBytes,
                    PdfContent = pdfPath is null ? pdf : null,
                    PdfPath = pdfPath,
                    TemplateVersion = version.TemplateVersion,
                    CreatedAtUtc = version.CreatedAtUtc,
                    CreatedBy = Limit(version.CreatedBy, 200),
                    Reason = Limit(version.Reason, 500)
                });
        }

        foreach (var decaEvent in deca.Events)
            entity.Events.Add(ToEntity(deca.Id, decaEvent));

        context.DecaDocuments.Add(entity);

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<byte[]?> GetPdfAsync(
        string tenantId,
        string decaId,
        int? version = null,
        CancellationToken cancellationToken = default)
    {
        await using var context =
            await _contextFactory.CreateDbContextAsync(cancellationToken);

        var row =
            await context.DecaVersions
                .AsNoTracking()
                .Where(x =>
                    x.TenantId == tenantId &&
                    x.DecaId == decaId &&
                    x.Version == (version ?? x.Deca.CurrentVersion))
                .Select(x => new
                {
                    x.PdfPath,
                    x.PdfContent
                })
                .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
            return null;

        if (row.PdfContent is not null)
            return row.PdfContent;

        if (row.PdfPath is null)
            return null;

        // Un PDF guardado en el almacén con Deca:Storage quitado después:
        // la configuración no puede dejar de leer lo ya emitido.
        if (_store is null)
        {
            throw new InvalidOperationException(
                $"El PDF del DeCA {decaId} está en el almacén externo ({row.PdfPath}) y Deca:Storage no está configurado.");
        }

        return await _store.ReadAsync(row.PdfPath, cancellationToken);
    }

    public async Task<DecaDocument?> GetAsync(
        string tenantId,
        string decaId,
        CancellationToken cancellationToken = default)
    {
        await using var context =
            await _contextFactory.CreateDbContextAsync(cancellationToken);

        var entity =
            await context.DecaDocuments
                .AsNoTracking()
                .Include(x => x.Events)
                .FirstOrDefaultAsync(
                    x => x.TenantId == tenantId && x.DecaId == decaId,
                    cancellationToken);

        if (entity is null)
            return null;

        // Las versiones sin el PDF: hasta 5 MB cada una, y para el detalle
        // basta con su hash y su tamaño.
        entity.Versions =
            await context.DecaVersions
                .AsNoTracking()
                .Where(x => x.TenantId == tenantId && x.DecaId == decaId)
                .Select(x => new DecaVersionEntity
                {
                    DecaId = x.DecaId,
                    Version = x.Version,
                    TenantId = x.TenantId,
                    DataJson = x.DataJson,
                    Sha256 = x.Sha256,
                    SizeBytes = x.SizeBytes,
                    PdfPath = x.PdfPath,
                    CreatedAtUtc = x.CreatedAtUtc,
                    CreatedBy = x.CreatedBy,
                    Reason = x.Reason,
                    TemplateVersion = x.TemplateVersion
                })
                .ToListAsync(cancellationToken);

        return ToDomain(entity);
    }

    public async Task<DecaPublicTarget?> FindByPublicCodeAsync(
        string publicCode,
        CancellationToken cancellationToken = default)
    {
        if (!DecaPublicCode.IsWellFormed(publicCode))
            return null;

        await using var context =
            await _contextFactory.CreateDbContextAsync(cancellationToken);

        return await context.DecaDocuments
            .AsNoTracking()
            .Where(x => x.PublicCode == publicCode)
            .Select(x => new DecaPublicTarget(
                x.TenantId,
                x.DecaId,
                x.Number,
                x.CurrentVersion,
                x.PublicUntilUtc,
                x.Versions
                    .Where(v => v.Version == x.CurrentVersion)
                    .Select(v => v.Sha256)
                    .FirstOrDefault() ?? string.Empty))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<DecaPage> SearchAsync(
        string tenantId,
        DecaQuery query,
        CancellationToken cancellationToken = default)
    {
        await using var context =
            await _contextFactory.CreateDbContextAsync(cancellationToken);

        var rows =
            context.DecaDocuments
                .AsNoTracking()
                .Where(x => x.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            var plate = new string(term.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

            rows = rows.Where(x =>
                x.Number.Contains(term) ||
                (x.Reference != null && x.Reference.Contains(term)) ||
                (x.ShipperName != null && x.ShipperName.Contains(term)) ||
                (x.CarrierName != null && x.CarrierName.Contains(term)) ||
                (plate.Length > 0 && x.TractorPlate != null && x.TractorPlate.Contains(plate)));
        }

        if (query.From is { } from)
            rows = rows.Where(x => x.TransportDate >= from);

        if (query.To is { } to)
            rows = rows.Where(x => x.TransportDate <= to);

        var total = await rows.CountAsync(cancellationToken);

        var items =
            await rows
                .OrderByDescending(x => x.IssuedAtUtc)
                .ThenByDescending(x => x.DecaId)
                .Skip(Math.Max(0, query.Skip))
                .Take(Math.Clamp(query.Take, 1, MaxTake))
                .Select(x => new DecaSummary
                {
                    Id = x.DecaId,
                    Number = x.Number,
                    Status = x.Status == nameof(DecaStatus.Finished)
                        ? DecaStatus.Finished
                        : DecaStatus.Issued,
                    Reference = x.Reference,
                    ShipperName = x.ShipperName,
                    CarrierName = x.CarrierName,
                    TractorPlate = x.TractorPlate,
                    TransportDate = x.TransportDate,
                    ShipmentCount = x.ShipmentCount,
                    CurrentVersion = x.CurrentVersion,
                    IssuedAtUtc = x.IssuedAtUtc,
                    PublicUntilUtc = x.PublicUntilUtc
                })
                .ToListAsync(cancellationToken);

        return new DecaPage
        {
            // Sin esto salen sin "Z" en el JSON y un ERP las leería como
            // hora local.
            Items =
                items
                    .Select(x => new DecaSummary
                    {
                        Id = x.Id,
                        Number = x.Number,
                        Status = x.Status,
                        Reference = x.Reference,
                        ShipperName = x.ShipperName,
                        CarrierName = x.CarrierName,
                        TractorPlate = x.TractorPlate,
                        TransportDate = x.TransportDate,
                        ShipmentCount = x.ShipmentCount,
                        CurrentVersion = x.CurrentVersion,
                        IssuedAtUtc = AsUtc(x.IssuedAtUtc),
                        PublicUntilUtc = AsUtc(x.PublicUntilUtc)
                    })
                    .ToList(),
            Total = total
        };
    }

    public async Task<bool> FinishServiceAsync(
        string tenantId,
        string decaId,
        DateTime endedAtUtc,
        DateTime publicUntilUtc,
        string? actor,
        CancellationToken cancellationToken = default)
    {
        await using var context =
            await _contextFactory.CreateDbContextAsync(cancellationToken);

        var entity =
            await context.DecaDocuments
                .FirstOrDefaultAsync(
                    x => x.TenantId == tenantId && x.DecaId == decaId,
                    cancellationToken);

        if (entity is null ||
            entity.Status != nameof(DecaStatus.Issued))
        {
            return false;
        }

        entity.Status = nameof(DecaStatus.Finished);
        entity.ServiceEndedAtUtc = endedAtUtc;
        entity.PublicUntilUtc = publicUntilUtc;

        context.DecaEvents.Add(
            ToEntity(
                decaId,
                new DecaEvent
                {
                    Kind = DecaEventKind.ServiceFinished,
                    AtUtc = DateTime.UtcNow,
                    Actor = actor,
                    Detail = $"El QR descarga hasta {publicUntilUtc:yyyy-MM-dd HH:mm} UTC."
                }));

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Otro lo ha terminado a la vez: el resultado es el mismo.
            return false;
        }

        return true;
    }

    public async Task AddEventAsync(
        string tenantId,
        string decaId,
        DecaEvent decaEvent,
        CancellationToken cancellationToken = default)
    {
        await using var context =
            await _contextFactory.CreateDbContextAsync(cancellationToken);

        var exists =
            await context.DecaDocuments
                .AnyAsync(
                    x => x.TenantId == tenantId && x.DecaId == decaId,
                    cancellationToken);

        if (!exists)
            return;

        context.DecaEvents.Add(ToEntity(decaId, decaEvent));

        await context.SaveChangesAsync(cancellationToken);
    }

    // ------------------------------------------------------------ mapeo

    private static DecaDocument ToDomain(
        DecaDocumentEntity entity) =>
        new()
        {
            Id = entity.DecaId,
            TenantId = entity.TenantId,
            Number = entity.Number,
            PublicCode = entity.PublicCode,
            Status = Enum.TryParse<DecaStatus>(entity.Status, out var status)
                ? status
                : DecaStatus.Issued,
            Data = Deserialize(entity.DataJson),
            CurrentVersion = entity.CurrentVersion,
            IssuedAtUtc = AsUtc(entity.IssuedAtUtc),
            IssuedBy = entity.IssuedBy,
            ServiceEndedAtUtc = AsUtc(entity.ServiceEndedAtUtc),
            PublicUntilUtc = AsUtc(entity.PublicUntilUtc),
            RetainUntilUtc = AsUtc(entity.RetainUntilUtc),

            Versions =
                entity.Versions
                    .OrderBy(x => x.Version)
                    .Select(x => new DecaVersion
                    {
                        Version = x.Version,
                        Data = Deserialize(x.DataJson),
                        Sha256 = x.Sha256,
                        SizeBytes = x.SizeBytes,
                        CreatedAtUtc = AsUtc(x.CreatedAtUtc),
                        CreatedBy = x.CreatedBy,
                        Reason = x.Reason,
                        TemplateVersion = x.TemplateVersion
                    })
                    .ToList(),

            Events =
                entity.Events
                    .OrderBy(x => x.AtUtc)
                    .ThenBy(x => x.EventId)
                    .Select(x => new DecaEvent
                    {
                        Kind = Enum.TryParse<DecaEventKind>(x.Kind, out var kind)
                            ? kind
                            : DecaEventKind.Issued,
                        AtUtc = AsUtc(x.AtUtc),
                        Actor = x.Actor,
                        Detail = x.Detail
                    })
                    .ToList()
        };

    private static DecaEventEntity ToEntity(
        string decaId,
        DecaEvent decaEvent) =>
        new()
        {
            DecaId = decaId,
            Kind = decaEvent.Kind.ToString(),
            AtUtc = decaEvent.AtUtc,
            Actor = Limit(decaEvent.Actor, 200),
            Detail = Limit(decaEvent.Detail, 1000)
        };

    private static void CopySearchColumns(
        DecaDocumentEntity entity,
        DecaData data)
    {
        entity.Reference = Limit(data.Reference, 200);
        entity.ShipperName = Limit(data.Shipper.Name, 200);
        entity.ShipperTaxId = Limit(data.Shipper.TaxId, 20);
        entity.CarrierName = Limit(data.Carrier.Name, 200);
        entity.CarrierTaxId = Limit(data.Carrier.TaxId, 20);
        entity.TractorPlate = Limit(data.Vehicle.TractorPlate, 20);
        entity.TransportDate = data.TransportDate;
        entity.ShipmentCount = data.Shipments.Count;
    }

    /// <summary>
    /// DECA-2026-000123 → (2026, 123). Lo escribe DecaIssuer.
    /// </summary>
    internal static (int Year, int Sequence) ParseNumber(
        string number)
    {
        var parts = number.Split('-');

        if (parts.Length == 3 &&
            int.TryParse(parts[1], out var year) &&
            int.TryParse(parts[2], out var sequence))
        {
            return (year, sequence);
        }

        throw new InvalidOperationException($"Número de DeCA con formato inesperado: '{number}'.");
    }

    private static string Serialize(
        DecaData data) =>
        JsonSerializer.Serialize(data, JsonOptions);

    private static DecaData Deserialize(
        string json) =>
        JsonSerializer.Deserialize<DecaData>(json, JsonOptions) ?? new DecaData();

    private static string? Limit(
        string? value,
        int max) =>
        value is null || value.Length <= max
            ? value
            : value[..max];

    private static DateTime AsUtc(
        DateTime value) =>
        DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static DateTime? AsUtc(
        DateTime? value) =>
        value is null
            ? null
            : DateTime.SpecifyKind(value.Value, DateTimeKind.Utc);

    private static void Add(
        DbCommand command,
        string name,
        DbType type,
        object value)
    {
        var parameter = command.CreateParameter();

        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value;

        command.Parameters.Add(parameter);
    }
}
