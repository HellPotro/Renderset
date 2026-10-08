using System.Globalization;
using System.Security.Cryptography;
using Renderset.Core.Rendering;
using Renderset.Core.Rendering.Pdf;
using Renderset.Core.Services;
using Renderset.Core.Sharing;
using Renderset.Core.Tenancy;
using Renderset.Core.Themes;
using Renderset.Deca.Validation;

namespace Renderset.Deca.Issuing;

public interface IDecaIssuer
{
    /// <summary>
    /// Valida y emite. O sale un DeCA completo (PDF guardado, QR activo) o
    /// no sale nada: no hay DeCA "a medias" cuyo QR no descargue.
    /// </summary>
    Task<DecaIssueResult> IssueAsync(
        string tenantId,
        DecaData data,
        string? actor,
        CancellationToken cancellationToken = default);
}

public sealed class DecaIssueResult
{
    public DecaDocument? Deca { get; private init; }

    public DecaValidationResult Validation { get; private init; } = new();

    /// <summary>
    /// Fallo que no es de los datos (conversor de PDF, tamaño, dominio).
    /// </summary>
    public DecaIssue? Failure { get; private init; }

    public bool Succeeded =>
        Deca is not null;

    public static DecaIssueResult Invalid(
        DecaValidationResult validation) =>
        new() { Validation = validation };

    public static DecaIssueResult Failed(
        DecaIssue failure,
        DecaValidationResult? validation = null) =>
        new()
        {
            Failure = failure,
            Validation = validation ?? new DecaValidationResult()
        };

    public static DecaIssueResult Issued(
        DecaDocument deca,
        DecaValidationResult validation) =>
        new()
        {
            Deca = deca,
            Validation = validation
        };

    /// <summary>
    /// Errores en el formato de la API ({ code, path, message }).
    /// </summary>
    public IReadOnlyList<RenderValidationError> ToApiErrors() =>
        (Failure is null ? Validation.Errors : new[] { Failure })
            .Select(x => new RenderValidationError
            {
                Code = x.Code,
                Path = x.Path,
                Message = x.Message
            })
            .ToList();
}

/// <summary>
/// Emite un DeCA:
///
///   1. Normaliza y valida (las mismas reglas que ve el portal).
///   2. Reserva el número y el código del QR: los dos van impresos.
///   3. Pinta el HTML con la plantilla DeCA y la marca del tenant.
///   4. Lo convierte a PDF AHORA, con fecha y hora de creación en los
///      metadatos. El QR nunca genera nada: sólo sirve lo que se guardó.
///   5. Comprueba el tamaño (5 MB) y calcula el SHA-256.
///   6. Guarda el DeCA con su PDF en la base de datos de DeCA. Hasta este
///      último paso el código del QR no lleva a ningún sitio.
///
/// Si el PDF falla, no se emite: un DeCA cuyo QR no descarga no cumple.
/// </summary>
public sealed class DecaIssuer
    : IDecaIssuer
{
    private readonly IDecaRepository _decas;
    private readonly IReportDocumentRenderer _renderer;
    private readonly IPdfConverter _pdf;
    private readonly PdfOptions _pdfOptions;
    private readonly IDocumentSharingSettingsRepository _sharing;
    private readonly DecaOptions _options;
    private readonly TimeProvider _time;
    private readonly IHtmlAssetInliner? _assetInliner;
    private readonly IDecaTemplateRepository? _templates;
    private readonly IReportThemeRepository? _themes;

    public DecaIssuer(
        IDecaRepository decas,
        IReportDocumentRenderer renderer,
        IPdfConverter pdf,
        PdfOptions pdfOptions,
        IDocumentSharingSettingsRepository sharing,
        DecaOptions options,
        TimeProvider time,
        IHtmlAssetInliner? assetInliner = null,
        IDecaTemplateRepository? templates = null,
        IReportThemeRepository? themes = null)
    {
        _decas = decas;
        _renderer = renderer;
        _pdf = pdf;
        _pdfOptions = pdfOptions;
        _sharing = sharing;
        _options = options;
        _time = time;
        _assetInliner = assetInliner;
        _templates = templates;
        _themes = themes;
    }

    public async Task<DecaIssueResult> IssueAsync(
        string tenantId,
        DecaData data,
        string? actor,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        // La hora de emisión se fija una vez: es la que se valida contra el
        // inicio del transporte, la que se imprime y la de los metadatos.
        var now = _time.GetUtcNow();

        var normalized =
            DecaNormalizer.Normalize(
                data ?? new DecaData());

        var validation =
            DecaValidator.Validate(
                normalized,
                now,
                _options.Zone);

        if (!validation.IsValid)
            return DecaIssueResult.Invalid(validation);

        if (!_pdf.IsAvailable)
        {
            return DecaIssueResult.Failed(
                new DecaIssue(
                    "deca.pdf_unavailable",
                    "",
                    "No hay conversor de PDF configurado: sin PDF no se puede emitir un DeCA."),
                validation);
        }

        var publicCode = DecaPublicCode.New();
        var publicUrl = _options.PublicUrl(publicCode);

        if (publicUrl is null)
        {
            return DecaIssueResult.Failed(
                new DecaIssue(
                    "deca.public_url_missing",
                    "",
                    "Falta el dominio público del QR (Deca:PublicBaseUrl) o no es HTTPS."),
                validation);
        }

        var settings =
            await _sharing.GetAsync(
                tenantId,
                cancellationToken);

        // Nulo = el tenant no existe. Mejor pararlo aquí que generar el PDF
        // y fallar al guardar.
        if (settings is null)
        {
            return DecaIssueResult.Failed(
                new DecaIssue(
                    "deca.tenant_not_found",
                    "",
                    $"No existe el tenant '{tenantId}'."),
                validation);
        }

        var branding = settings.Normalized().ToBranding(tenantId);

        // Diseño del tenant, corregido por el guardián. Si no vale (oculta
        // algo obligatorio), el base: nunca un DeCA incompleto.
        var template =
            _templates is null
                ? null
                : await _templates.GetCurrentAsync(tenantId, cancellationToken);

        var design = DecaTemplateGuard.Resolve(template, branding);

        // Un diseño enlazado a un tema de empresa (Marca → Temas) se pinta
        // con el tema vivo, como los presets de RenderSet; si el tema ya no
        // existe, con la copia que guarda el diseño.
        if (_themes is not null &&
            design.TemplateVersion != DecaTemplate.BaseVersion &&
            template?.Configuration?.ThemeId is { Length: > 0 } themeId)
        {
            var linked = await _themes.GetByIdAsync(tenantId, themeId, cancellationToken);

            if (linked is not null)
                design = design with { Theme = linked.Theme };
        }

        var local = TimeZoneInfo.ConvertTime(now, _options.Zone);

        var sequence =
            await _decas.NextSequenceAsync(
                tenantId,
                local.Year,
                cancellationToken);

        var number = FormatNumber(local.Year, sequence);
        var decaId = Guid.NewGuid().ToString("N");

        var html =
            await RenderHtmlAsync(
                normalized,
                number,
                local,
                publicUrl,
                decaId,
                design,
                cancellationToken);

        byte[] pdf;

        try
        {
            pdf =
                await _pdf.ConvertHtmlAsync(
                    html.PdfHtml,
                    html.Page.WithMetadata(
                        Metadata(normalized, number, local)),
                    cancellationToken);
        }
        catch (PdfConversionException exception)
        {
            return DecaIssueResult.Failed(
                new DecaIssue(
                    "deca.pdf_failed",
                    "",
                    $"No se ha podido generar el PDF; el DeCA no se ha emitido. {exception.Message}"),
                validation);
        }

        if (pdf.LongLength > DecaOptions.MaxPdfBytes)
        {
            return DecaIssueResult.Failed(
                new DecaIssue(
                    "deca.pdf_too_large",
                    "",
                    $"El PDF ocupa {pdf.LongLength / 1024.0 / 1024.0:0.0} MB y la norma admite 5 MB. " +
                    "Suele ser el logo: súbelo más pequeño en Compartir → Página pública."),
                validation);
        }

        var sha256 = Convert.ToHexString(SHA256.HashData(pdf)).ToLowerInvariant();
        var issuedAtUtc = now.UtcDateTime;

        var deca =
            new DecaDocument
            {
                Id = decaId,
                TenantId = tenantId,
                Number = number,
                PublicCode = publicCode,
                Status = DecaStatus.Issued,
                Data = normalized,
                CurrentVersion = 1,
                IssuedAtUtc = issuedAtUtc,
                IssuedBy = actor,
                RetainUntilUtc = DecaAvailability.RetainUntil(issuedAtUtc, _options),
                Versions =
                [
                    new DecaVersion
                    {
                        Version = 1,
                        Data = normalized,
                        Sha256 = sha256,
                        SizeBytes = pdf.LongLength,
                        CreatedAtUtc = issuedAtUtc,
                        CreatedBy = actor,
                        TemplateVersion = design.TemplateVersion
                    }
                ],
                Events =
                [
                    new DecaEvent
                    {
                        Kind = DecaEventKind.Issued,
                        AtUtc = issuedAtUtc,
                        Actor = actor,
                        Detail = $"Versión 1 · {pdf.LongLength / 1024.0:0} KB · SHA-256 {sha256} · " +
                                 (design.Rejected.Count > 0
                                     ? $"diseño base: el propio (v{template?.Version}) oculta {string.Join(", ", design.Rejected)}"
                                     : design.TemplateVersion == DecaTemplate.BaseVersion
                                         ? "diseño base"
                                         : $"diseño v{design.TemplateVersion}")
                    }
                ]
            };

        await _decas.AddAsync(
            deca,
            pdf,
            cancellationToken);

        return DecaIssueResult.Issued(deca, validation);
    }

    /// <param name="PdfHtml">El documento sin el pie, que el conversor repite en cada página.</param>
    private sealed record RenderedHtml(
        string PdfHtml,
        PdfPageOptions Page);

    private async Task<RenderedHtml> RenderHtmlAsync(
        DecaData data,
        string number,
        DateTimeOffset issuedAtLocal,
        string publicUrl,
        string decaId,
        DecaTemplateGuard.Design design,
        CancellationToken cancellationToken)
    {
        var json =
            DecaReportData.Build(
                data,
                number,
                version: 1,
                issuedAtLocal,
                publicUrl);

        var html =
            await _renderer.RenderHtmlAsync(
                design.Resolved,
                json,
                design.Theme,
                number,
                includeDocumentData: false,
                new ReportDocumentContext
                {
                    DocumentId = decaId,

                    // El QR del DeCA es el enlace corto que descarga el PDF.
                    DocumentUrl = publicUrl,
                    PdfUrl = publicUrl
                },
                cancellationToken);

        // El logo va dentro: el PDF no puede depender de que mañana siga
        // existiendo la URL de la imagen.
        if (_assetInliner is not null)
            html = await _assetInliner.InlineAsync(html, cancellationToken);

        // Igual que DocumentPdfService: el pie se saca para que el conversor
        // lo repita en cada página. Del DeCA sólo se guarda el PDF: es el
        // documento legal, y el HTML no se vuelve a servir.
        var page = _pdfOptions.Page;
        var pdfHtml = html;
        var footer = ReportPageFooter.Extract(html);

        if (footer is not null)
        {
            pdfHtml = footer.Html;
            page = page.With(footer.FooterHtml, footer.HeightMm);
        }

        return new RenderedHtml(pdfHtml, page);
    }

    /// <summary>
    /// Fecha y hora de creación y de modificación (la misma al emitir), más
    /// lo que identifica el documento al abrirlo en cualquier visor.
    /// </summary>
    internal static IReadOnlyDictionary<string, string> Metadata(
        DecaData data,
        string number,
        DateTimeOffset issuedAtLocal)
    {
        var timestamp = issuedAtLocal.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture);

        return new Dictionary<string, string>
        {
            ["Title"] = $"DeCA {number}",
            ["Subject"] = "Documento electrónico de control administrativo",
            ["Author"] = data.Shipper.Name ?? string.Empty,
            ["Creator"] = "RenderSet DeCA",
            ["Keywords"] = $"DeCA; {number}; {data.Shipper.TaxId}; {data.Carrier.TaxId}",
            ["CreationDate"] = timestamp,
            ["ModDate"] = timestamp
        };
    }

    internal static string FormatNumber(
        int year,
        int sequence) =>
        $"DECA-{year}-{sequence:000000}";
}
