using Renderset.Core.Rendering;

namespace Renderset.Core.Sharing;

public interface IDocumentBundleService
{
    Task<DocumentBundleLinkResult> CreateAsync(
        string tenantId,
        CreateDocumentBundleRequest request,
        CancellationToken cancellationToken = default);

    Task<DocumentBundleLinkResult> RenewLinkAsync(
        string tenantId,
        Guid bundleId,
        RenewDocumentBundleLinkRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> RevokeAsync(
        string tenantId,
        Guid bundleId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Token en claro del enlace actual, para construir la URL desde la
    /// gestión. Nulo si no se puede recuperar.
    /// </summary>
    string? RecoverToken(
        DocumentBundle bundle);

    Task<DocumentBundleOpenResult> OpenAsync(
        Guid bundleId,
        string? token,
        CancellationToken cancellationToken = default);

    Task<BundledDocument?> GetDocumentAsync(
        DocumentBundle bundle,
        int position,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BundledDocument>> GetDocumentsAsync(
        DocumentBundle bundle,
        CancellationToken cancellationToken = default);

    Task RecordAccessAsync(
        DocumentBundle bundle,
        DocumentBundleAccessKind kind,
        int? position,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default);
}


/// <summary>
/// Crea bundles, emite y renueva sus enlaces, y resuelve el acceso público.
///
/// Las validaciones devuelven <see cref="RenderValidationError"/>, el mismo
/// formato que /api/render: quien integra trata los errores de los dos
/// endpoints con el mismo código.
/// </summary>
public sealed class DocumentBundleService
    : IDocumentBundleService
{
    public const int MaxTitleLength = 200;

    public const int MaxMessageLength = 2000;

    public const int MaxDisplayNameLength = 200;

    private readonly IDocumentBundleRepository _bundles;
    private readonly IRenderedDocumentRepository _documents;
    private readonly IReportRenderService _renderService;
    private readonly IDocumentSharingSettingsRepository _settings;
    private readonly IBundleTokenProtector _protector;
    private readonly DocumentSharingOptions _options;
    private readonly TimeProvider _time;

    public DocumentBundleService(
        IDocumentBundleRepository bundles,
        IRenderedDocumentRepository documents,
        IReportRenderService renderService,
        IDocumentSharingSettingsRepository settings,
        IBundleTokenProtector protector,
        DocumentSharingOptions options,
        TimeProvider time)
    {
        _protector = protector;
        _bundles = bundles;
        _documents = documents;
        _renderService = renderService;
        _settings = settings;
        _options = options;
        _time = time;
    }

    // ------------------------------------------------------------ gestión

    public async Task<DocumentBundleLinkResult> CreateAsync(
        string tenantId,
        CreateDocumentBundleRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentNullException.ThrowIfNull(request);

        var now = UtcNow();
        var errors = new List<RenderValidationError>();

        // Todo lo que se puede comprobar sin renderizar se comprueba antes:
        // no tiene sentido emitir tres documentos para luego rechazar el
        // bundle por un título vacío.
        ValidateRequest(request, errors);

        var expiresAtUtc =
            ResolveExpiration(
                now,
                request.ExpiresAtUtc,
                request.ExpiresInDays,
                await DefaultExpirationDaysAsync(tenantId, cancellationToken),
                errors);

        if (errors.Count > 0)
            return DocumentBundleLinkResult.Failure(errors);

        var documentIds =
            await ResolveDocumentIdsAsync(
                tenantId,
                request.Items,
                errors,
                cancellationToken);

        if (errors.Count > 0)
            return DocumentBundleLinkResult.Failure(errors);

        var summaries =
            await _documents.GetSummariesAsync(
                tenantId,
                documentIds,
                cancellationToken);

        var byId =
            summaries.ToDictionary(
                x => x.Id,
                StringComparer.OrdinalIgnoreCase);

        var items = new List<DocumentBundleItem>(documentIds.Count);

        for (var i = 0; i < documentIds.Count; i++)
        {
            if (!byId.TryGetValue(documentIds[i], out var summary))
            {
                errors.Add(Error(
                    "bundle.document_not_found",
                    $"No existe el documento '{documentIds[i]}' en el tenant '{tenantId}'.",
                    $"items[{i}].documentId"));

                continue;
            }

            items.Add(new DocumentBundleItem
            {
                Position = i + 1,
                DocumentId = summary.Id,
                DisplayName = ResolveDisplayName(
                    request.Items[i].DisplayName,
                    summary.FileName)
            });
        }

        if (errors.Count > 0)
            return DocumentBundleLinkResult.Failure(errors);

        var token = BundleToken.Generate();

        var bundle =
            new DocumentBundle
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Title = request.Title!.Trim(),
                Message = string.IsNullOrWhiteSpace(request.Message)
                    ? null
                    : request.Message.Trim(),
                Culture = ResolveCulture(
                    request.Culture,
                    items,
                    byId),
                TokenHash = BundleToken.Hash(token),
                ProtectedToken = _protector.Protect(token),
                TokenIssuedAtUtc = now,
                ExpiresAtUtc = expiresAtUtc!.Value,
                CreatedAtUtc = now,
                AllowDataDownload = request.AllowDataDownload,
                Items = items
            };

        await _bundles.CreateAsync(
            bundle,
            cancellationToken);

        return DocumentBundleLinkResult.Success(bundle, token);
    }

    public async Task<DocumentBundleLinkResult> RenewLinkAsync(
        string tenantId,
        Guid bundleId,
        RenewDocumentBundleLinkRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentNullException.ThrowIfNull(request);

        var now = UtcNow();
        var errors = new List<RenderValidationError>();

        var expiresAtUtc =
            ResolveExpiration(
                now,
                request.ExpiresAtUtc,
                request.ExpiresInDays,
                await DefaultExpirationDaysAsync(tenantId, cancellationToken),
                errors);

        if (errors.Count > 0)
            return DocumentBundleLinkResult.Failure(errors);

        var token = BundleToken.Generate();

        var replaced =
            await _bundles.ReplaceLinkAsync(
                tenantId,
                bundleId,
                BundleToken.Hash(token),
                _protector.Protect(token),
                now,
                expiresAtUtc!.Value,
                cancellationToken);

        if (!replaced)
            return DocumentBundleLinkResult.Missing();

        var bundle =
            await _bundles.GetAsync(
                tenantId,
                bundleId,
                cancellationToken);

        return bundle is null
            ? DocumentBundleLinkResult.Missing()
            : DocumentBundleLinkResult.Success(bundle, token);
    }

    public Task<bool> RevokeAsync(
        string tenantId,
        Guid bundleId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        return _bundles.RevokeAsync(
            tenantId,
            bundleId,
            UtcNow(),
            cancellationToken);
    }

    public string? RecoverToken(
        DocumentBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);

        if (string.IsNullOrWhiteSpace(bundle.ProtectedToken))
            return null;

        var token = _protector.Unprotect(bundle.ProtectedToken);

        // Se comprueba contra el hash: si el valor cifrado no corresponde al
        // token vigente (una renovación a medias, una fila tocada a mano), es
        // preferible no dar enlace a dar uno que no abre.
        return BundleToken.Matches(token, bundle.TokenHash)
            ? token
            : null;
    }

    // ------------------------------------------------------------ público

    public async Task<DocumentBundleOpenResult> OpenAsync(
        Guid bundleId,
        string? token,
        CancellationToken cancellationToken = default)
    {
        // Un token mal formado no llega a la base de datos.
        if (!BundleToken.IsWellFormed(token))
            return NotFound();

        var bundle =
            await _bundles.FindAsync(
                bundleId,
                cancellationToken);

        if (bundle is null ||
            !BundleToken.Matches(token, bundle.TokenHash))
        {
            return NotFound();
        }

        var status =
            bundle.GetStatus(UtcNow()) switch
            {
                DocumentBundleStatus.Revoked => DocumentBundleOpenStatus.Revoked,
                DocumentBundleStatus.Expired => DocumentBundleOpenStatus.Expired,
                _ => DocumentBundleOpenStatus.Available
            };

        return new DocumentBundleOpenResult
        {
            Status = status,
            Bundle = bundle
        };
    }

    public async Task<BundledDocument?> GetDocumentAsync(
        DocumentBundle bundle,
        int position,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bundle);

        var item =
            bundle.Items.FirstOrDefault(x => x.Position == position);

        if (item is null)
            return null;

        var document =
            await _documents.GetByIdAsync(
                bundle.TenantId,
                item.DocumentId,
                cancellationToken);

        return document is null
            ? null
            : new BundledDocument
            {
                Item = item,
                Document = document
            };
    }

    public async Task<IReadOnlyList<BundledDocument>> GetDocumentsAsync(
        DocumentBundle bundle,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bundle);

        var result = new List<BundledDocument>(bundle.Items.Count);

        foreach (var item in bundle.Items.OrderBy(x => x.Position))
        {
            var document =
                await GetDocumentAsync(
                    bundle,
                    item.Position,
                    cancellationToken);

            if (document is not null)
                result.Add(document);
        }

        return result;
    }

    public Task RecordAccessAsync(
        DocumentBundle bundle,
        DocumentBundleAccessKind kind,
        int? position,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bundle);

        return _bundles.RecordAccessAsync(
            new DocumentBundleAccess
            {
                BundleId = bundle.Id,
                TenantId = bundle.TenantId,
                Kind = kind,
                Position = position,
                AccessedAtUtc = UtcNow(),
                IpAddress = Truncate(ipAddress, 64),
                UserAgent = Truncate(userAgent, 512)
            },
            cancellationToken);
    }

    // ------------------------------------------------------------ interno

    private void ValidateRequest(
        CreateDocumentBundleRequest request,
        List<RenderValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            errors.Add(Error(
                "bundle.title_required",
                "Falta el título del bundle. Es lo que ve el cliente arriba de la página.",
                "title"));
        }
        else if (request.Title.Trim().Length > MaxTitleLength)
        {
            errors.Add(Error(
                "bundle.title_too_long",
                $"El título no puede pasar de {MaxTitleLength} caracteres.",
                "title",
                expected: $"<= {MaxTitleLength}",
                actual: request.Title.Trim().Length.ToString()));
        }

        if (request.Message is { Length: > MaxMessageLength })
        {
            errors.Add(Error(
                "bundle.message_too_long",
                $"El mensaje no puede pasar de {MaxMessageLength} caracteres.",
                "message",
                expected: $"<= {MaxMessageLength}",
                actual: request.Message.Length.ToString()));
        }

        var items = request.Items ?? [];

        if (items.Count == 0)
        {
            errors.Add(Error(
                "bundle.items_required",
                "El bundle necesita al menos un documento.",
                "items"));

            return;
        }

        if (items.Count > _options.MaxDocumentsPerBundle)
        {
            errors.Add(Error(
                "bundle.too_many_items",
                $"Un bundle admite como máximo {_options.MaxDocumentsPerBundle} documentos.",
                "items",
                expected: $"<= {_options.MaxDocumentsPerBundle}",
                actual: items.Count.ToString()));
        }

        var seen =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var path = $"items[{i}]";

            if (item is null)
            {
                errors.Add(Error(
                    "bundle.item_required",
                    "Elemento vacío.",
                    path));

                continue;
            }

            var hasId = !string.IsNullOrWhiteSpace(item.DocumentId);
            var hasRender = item.Render is not null;

            if (hasId == hasRender)
            {
                errors.Add(Error(
                    "bundle.item_ambiguous",
                    "Cada elemento lleva 'documentId' (documento ya emitido) " +
                    "o 'render' (se emite ahora), y sólo uno de los dos.",
                    path));
            }

            if (hasId && !seen.Add(item.DocumentId!.Trim()))
            {
                errors.Add(Error(
                    "bundle.duplicate_document",
                    $"El documento '{item.DocumentId}' aparece dos veces.",
                    $"{path}.documentId"));
            }

            if (item.DisplayName is { Length: > MaxDisplayNameLength })
            {
                errors.Add(Error(
                    "bundle.display_name_too_long",
                    $"El nombre no puede pasar de {MaxDisplayNameLength} caracteres.",
                    $"{path}.displayName",
                    expected: $"<= {MaxDisplayNameLength}",
                    actual: item.DisplayName.Length.ToString()));
            }
        }
    }

    /// <summary>
    /// Devuelve los ids en el orden de la petición, emitiendo los que vienen
    /// como render.
    ///
    /// Si un render falla se para ahí y el bundle no se crea. Los documentos
    /// emitidos antes en la misma petición se quedan guardados sin bundle:
    /// son snapshots inmutables y sueltos, no hacen daño, y deshacerlos
    /// exigiría un borrado que hoy el repositorio no tiene.
    /// </summary>
    private async Task<List<string>> ResolveDocumentIdsAsync(
        string tenantId,
        IReadOnlyList<CreateDocumentBundleItem> items,
        List<RenderValidationError> errors,
        CancellationToken cancellationToken)
    {
        var ids = new List<string>(items.Count);

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];

            if (item.Render is null)
            {
                ids.Add(item.DocumentId!.Trim());
                continue;
            }

            // El visor enseña HTML y hoy el render sólo emite HTML. Se fija
            // aquí para que quien integra no tenga que saberlo: el valor por
            // defecto de RenderOutput es Pdf y daría un error que no tiene
            // nada que ver con lo que ha pedido.
            item.Render.Output ??= new RenderOutput();
            item.Render.Output.Format = RenderFormat.Html;
            item.Render.Output.BatchMode = RenderBatchMode.Single;

            var result =
                await _renderService.RenderAsync(
                    tenantId,
                    item.Render,
                    cancellationToken);

            if (!result.Succeeded)
            {
                errors.AddRange(
                    result.Errors.Select(error =>
                        new RenderValidationError
                        {
                            Code = error.Code,
                            Message = error.Message,
                            Path = string.IsNullOrWhiteSpace(error.Path)
                                ? $"items[{i}].render"
                                : $"items[{i}].render.{error.Path}",
                            Expected = error.Expected,
                            Actual = error.Actual
                        }));

                return ids;
            }

            ids.Add(result.Document!.Id);
        }

        return ids;
    }

    /// <summary>
    /// La del tenant si la tiene configurada y es válida; si no, la de
    /// DocumentSharing. Se acota al máximo por si el máximo se ha bajado
    /// después de configurarla.
    /// </summary>
    private async Task<int> DefaultExpirationDaysAsync(
        string tenantId,
        CancellationToken cancellationToken)
    {
        var settings =
            await _settings.GetAsync(
                tenantId,
                cancellationToken);

        var days =
            settings?.DefaultExpirationDays
            ?? _options.DefaultExpirationDays;

        return Math.Clamp(days, 1, _options.MaxExpirationDays);
    }

    private DateTime? ResolveExpiration(
        DateTime nowUtc,
        DateTime? expiresAtUtc,
        int? expiresInDays,
        int defaultDays,
        List<RenderValidationError> errors)
    {
        var max = nowUtc.AddDays(_options.MaxExpirationDays);

        if (expiresAtUtc.HasValue && expiresInDays.HasValue)
        {
            errors.Add(Error(
                "bundle.expiration_ambiguous",
                "Indica 'expiresAtUtc' o 'expiresInDays', no los dos.",
                "expiresAtUtc"));

            return null;
        }

        if (expiresInDays.HasValue)
        {
            if (expiresInDays.Value < 1 ||
                expiresInDays.Value > _options.MaxExpirationDays)
            {
                errors.Add(Error(
                    "bundle.expiration_out_of_range",
                    $"La caducidad tiene que estar entre 1 y {_options.MaxExpirationDays} días.",
                    "expiresInDays",
                    expected: $"1..{_options.MaxExpirationDays}",
                    actual: expiresInDays.Value.ToString()));

                return null;
            }

            return nowUtc.AddDays(expiresInDays.Value);
        }

        if (expiresAtUtc.HasValue)
        {
            var value = ToUtc(expiresAtUtc.Value);

            if (value <= nowUtc || value > max)
            {
                errors.Add(Error(
                    "bundle.expiration_out_of_range",
                    $"La caducidad tiene que ser futura y no pasar de {_options.MaxExpirationDays} días.",
                    "expiresAtUtc",
                    expected: $"> {nowUtc:O} y <= {max:O}",
                    actual: value.ToString("O")));

                return null;
            }

            return value;
        }

        return nowUtc.AddDays(defaultDays);
    }

    /// <summary>
    /// Un DateTime sin zona que llega por JSON se trata como UTC, que es lo
    /// que dice el nombre del campo.
    /// </summary>
    private static DateTime ToUtc(
        DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

    private static string ResolveDisplayName(
        string? requested,
        string fileName)
    {
        if (!string.IsNullOrWhiteSpace(requested))
            return requested.Trim();

        var name = Path.GetFileNameWithoutExtension(fileName);

        return string.IsNullOrWhiteSpace(name)
            ? fileName
            : name;
    }

    private static string ResolveCulture(
        string? requested,
        IReadOnlyList<DocumentBundleItem> items,
        IReadOnlyDictionary<string, RenderedDocumentSummary> summaries)
    {
        if (!string.IsNullOrWhiteSpace(requested))
            return requested.Trim();

        var first = items.FirstOrDefault();

        return first is not null &&
               summaries.TryGetValue(first.DocumentId, out var summary) &&
               !string.IsNullOrWhiteSpace(summary.Culture)
            ? summary.Culture
            : "es-ES";
    }

    private DateTime UtcNow() =>
        _time.GetUtcNow().UtcDateTime;

    private static DocumentBundleOpenResult NotFound() =>
        new()
        {
            Status = DocumentBundleOpenStatus.NotFound
        };

    private static string? Truncate(
        string? value,
        int length) =>
        value is null || value.Length <= length
            ? value
            : value[..length];

    private static RenderValidationError Error(
        string code,
        string message,
        string path,
        string? expected = null,
        string? actual = null) =>
        new()
        {
            Code = code,
            Message = message,
            Path = path,
            Expected = expected,
            Actual = actual
        };
}
