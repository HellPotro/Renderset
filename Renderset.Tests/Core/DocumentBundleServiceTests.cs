using FluentAssertions;
using Moq;
using Renderset.Core.Rendering;
using Renderset.Core.Sharing;

namespace Renderset.Tests.Core;

public sealed class DocumentBundleServiceTests
{
    private const string Tenant = "oranauto";

    private static readonly DateTime Now =
        new(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc);

    private readonly InMemoryBundleRepository _bundles = new();
    private readonly Mock<IRenderedDocumentRepository> _documents = new();
    private readonly Mock<IReportRenderService> _render = new();
    private readonly FixedTimeProvider _time = new(Now);
    private readonly DocumentSharingOptions _options = new();

    public DocumentBundleServiceTests()
    {
        GivenDocuments(
            Summary("doc-factura", "factura-F-0412.html", "es-ES"),
            Summary("doc-packing", "packing-list.html", "es-ES"));
    }

    private DocumentBundleService CreateSut() =>
        new(
            _bundles,
            _documents.Object,
            _render.Object,
            _options,
            _time);

    // ------------------------------------------------------------ crear

    [Fact]
    public async Task CreateAsync_ShouldStoreOnlyTheHashOfTheToken()
    {
        var result =
            await CreateSut().CreateAsync(
                Tenant,
                Request("doc-factura", "doc-packing"));

        result.Succeeded.Should().BeTrue();
        result.Token.Should().HaveLength(BundleToken.TextLength);

        var stored = _bundles.Single();

        stored.TokenHash.Should().Equal(BundleToken.Hash(result.Token!));
        stored.TokenHash.Should().NotEqual(
            System.Text.Encoding.ASCII.GetBytes(result.Token!));
    }

    [Fact]
    public async Task CreateAsync_ShouldKeepRequestOrderAndUseFileNameAsDefaultDisplayName()
    {
        var request = Request("doc-packing", "doc-factura");
        request.Items[1].DisplayName = "Factura F-2026-0412";

        var result =
            await CreateSut().CreateAsync(
                Tenant,
                request);

        result.Bundle!.Items
            .Select(x => (x.Position, x.DocumentId, x.DisplayName))
            .Should()
            .Equal(
                (1, "doc-packing", "packing-list"),
                (2, "doc-factura", "Factura F-2026-0412"));
    }

    [Fact]
    public async Task CreateAsync_ShouldApplyDefaultExpiration()
    {
        var result =
            await CreateSut().CreateAsync(
                Tenant,
                Request("doc-factura"));

        result.Bundle!.ExpiresAtUtc
            .Should()
            .Be(Now.AddDays(_options.DefaultExpirationDays));
    }

    [Fact]
    public async Task CreateAsync_ShouldTakeCultureFromFirstDocumentWhenNotRequested()
    {
        GivenDocuments(Summary("doc-en", "invoice.html", "en-GB"));

        var result =
            await CreateSut().CreateAsync(
                Tenant,
                Request("doc-en"));

        result.Bundle!.Culture.Should().Be("en-GB");
    }

    [Fact]
    public async Task CreateAsync_ShouldRejectUnknownDocumentsWithTheirPath()
    {
        var result =
            await CreateSut().CreateAsync(
                Tenant,
                Request("doc-factura", "no-existe"));

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle(x =>
            x.Code == "bundle.document_not_found" &&
            x.Path == "items[1].documentId");

        _bundles.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAsync_ShouldRejectMissingTitleBeforeRenderingAnything()
    {
        var request =
            new CreateDocumentBundleRequest
            {
                Items =
                [
                    new CreateDocumentBundleItem
                    {
                        Render = new RenderRequest { ReportId = "albaran" }
                    }
                ]
            };

        var result =
            await CreateSut().CreateAsync(
                Tenant,
                request);

        result.Errors.Should().Contain(x => x.Code == "bundle.title_required");

        _render.Verify(
            x => x.RenderAsync(
                It.IsAny<string>(),
                It.IsAny<RenderRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateAsync_ShouldRejectItemsWithBothDocumentIdAndRender()
    {
        var request = Request("doc-factura");
        request.Items[0].Render = new RenderRequest { ReportId = "albaran" };

        var result =
            await CreateSut().CreateAsync(
                Tenant,
                request);

        result.Errors.Should().ContainSingle(x =>
            x.Code == "bundle.item_ambiguous" &&
            x.Path == "items[0]");
    }

    [Fact]
    public async Task CreateAsync_ShouldRejectDuplicatedDocuments()
    {
        var result =
            await CreateSut().CreateAsync(
                Tenant,
                Request("doc-factura", "DOC-FACTURA"));

        result.Errors.Should().ContainSingle(x =>
            x.Code == "bundle.duplicate_document");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(366)]
    public async Task CreateAsync_ShouldRejectExpirationOutOfRange(
        int days)
    {
        var request = Request("doc-factura");
        request.ExpiresInDays = days;

        var result =
            await CreateSut().CreateAsync(
                Tenant,
                request);

        result.Errors.Should().ContainSingle(x =>
            x.Code == "bundle.expiration_out_of_range" &&
            x.Path == "expiresInDays");
    }

    [Fact]
    public async Task CreateAsync_ShouldRenderInlineItemsAsHtml()
    {
        RenderRequest? sent = null;

        _render
            .Setup(x => x.RenderAsync(
                Tenant,
                It.IsAny<RenderRequest>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, RenderRequest, CancellationToken>(
                (_, request, _) => sent = request)
            .ReturnsAsync(RenderResult.Success(Rendered("doc-nuevo")));

        GivenDocuments(
            Summary("doc-factura", "factura.html", "es-ES"),
            Summary("doc-nuevo", "albaran.html", "es-ES"));

        var request = Request("doc-factura");

        request.Items.Add(
            new CreateDocumentBundleItem
            {
                Render = new RenderRequest { ReportId = "albaran" }
            });

        var result =
            await CreateSut().CreateAsync(
                Tenant,
                request);

        result.Succeeded.Should().BeTrue();

        // RenderOutput es Pdf por defecto y hoy sólo se emite HTML: el
        // bundle lo fija para que quien integra no tenga que saberlo.
        sent!.Output.Format.Should().Be(RenderFormat.Html);

        result.Bundle!.Items
            .Select(x => x.DocumentId)
            .Should()
            .Equal("doc-factura", "doc-nuevo");
    }

    [Fact]
    public async Task CreateAsync_ShouldPrefixRenderErrorsWithTheItemPath()
    {
        _render
            .Setup(x => x.RenderAsync(
                Tenant,
                It.IsAny<RenderRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(RenderResult.Failure(
                "request.data_required",
                "Falta data.",
                path: "data"));

        var request =
            new CreateDocumentBundleRequest
            {
                Title = "Pedido 4512",
                Items =
                [
                    new CreateDocumentBundleItem
                    {
                        Render = new RenderRequest { ReportId = "albaran" }
                    }
                ]
            };

        var result =
            await CreateSut().CreateAsync(
                Tenant,
                request);

        result.Errors.Should().ContainSingle(x =>
            x.Code == "request.data_required" &&
            x.Path == "items[0].render.data");

        _bundles.Should().BeEmpty();
    }

    // ------------------------------------------------------------ abrir

    [Fact]
    public async Task OpenAsync_ShouldReturnTheBundleWithTheRightToken()
    {
        var sut = CreateSut();
        var created = await sut.CreateAsync(Tenant, Request("doc-factura"));

        var open =
            await sut.OpenAsync(
                created.Bundle!.Id,
                created.Token);

        open.Status.Should().Be(DocumentBundleOpenStatus.Available);
        open.Bundle!.Id.Should().Be(created.Bundle.Id);
    }

    [Fact]
    public async Task OpenAsync_ShouldNotRevealTheBundleWithAWrongToken()
    {
        var sut = CreateSut();
        var created = await sut.CreateAsync(Tenant, Request("doc-factura"));

        var open =
            await sut.OpenAsync(
                created.Bundle!.Id,
                BundleToken.Generate());

        open.Status.Should().Be(DocumentBundleOpenStatus.NotFound);
        open.Bundle.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("corto")]
    [InlineData("<script>alert(1)</script>_________________________")]
    public async Task OpenAsync_ShouldRejectMalformedTokensWithoutQueryingTheStore(
        string? token)
    {
        var open =
            await CreateSut().OpenAsync(
                Guid.NewGuid(),
                token);

        open.Status.Should().Be(DocumentBundleOpenStatus.NotFound);
        _bundles.FindCalls.Should().Be(0);
    }

    [Fact]
    public async Task OpenAsync_ShouldReportExpiredLinks()
    {
        var sut = CreateSut();
        var request = Request("doc-factura");
        request.ExpiresInDays = 1;

        var created = await sut.CreateAsync(Tenant, request);

        _time.Advance(TimeSpan.FromDays(1));

        var open =
            await sut.OpenAsync(
                created.Bundle!.Id,
                created.Token);

        open.Status.Should().Be(DocumentBundleOpenStatus.Expired);
        open.Bundle.Should().NotBeNull();
    }

    [Fact]
    public async Task OpenAsync_ShouldReportRevokedLinks()
    {
        var sut = CreateSut();
        var created = await sut.CreateAsync(Tenant, Request("doc-factura"));

        (await sut.RevokeAsync(Tenant, created.Bundle!.Id))
            .Should()
            .BeTrue();

        var open =
            await sut.OpenAsync(
                created.Bundle.Id,
                created.Token);

        open.Status.Should().Be(DocumentBundleOpenStatus.Revoked);
    }

    [Fact]
    public async Task RevokeAsync_ShouldNotTouchBundlesOfAnotherTenant()
    {
        var sut = CreateSut();
        var created = await sut.CreateAsync(Tenant, Request("doc-factura"));

        (await sut.RevokeAsync("otro-tenant", created.Bundle!.Id))
            .Should()
            .BeFalse();
    }

    // ------------------------------------------------------------ renovar

    [Fact]
    public async Task RenewLinkAsync_ShouldInvalidateThePreviousToken()
    {
        var sut = CreateSut();
        var created = await sut.CreateAsync(Tenant, Request("doc-factura"));

        await sut.RevokeAsync(Tenant, created.Bundle!.Id);

        var renewed =
            await sut.RenewLinkAsync(
                Tenant,
                created.Bundle.Id,
                new RenewDocumentBundleLinkRequest { ExpiresInDays = 7 });

        renewed.Succeeded.Should().BeTrue();
        renewed.Token.Should().NotBe(created.Token);

        (await sut.OpenAsync(created.Bundle.Id, created.Token))
            .Status.Should().Be(DocumentBundleOpenStatus.NotFound);

        var reopened =
            await sut.OpenAsync(created.Bundle.Id, renewed.Token);

        reopened.Status.Should().Be(DocumentBundleOpenStatus.Available);
        reopened.Bundle!.ExpiresAtUtc.Should().Be(Now.AddDays(7));
    }

    [Fact]
    public async Task RenewLinkAsync_ShouldReportMissingBundles()
    {
        var renewed =
            await CreateSut().RenewLinkAsync(
                Tenant,
                Guid.NewGuid(),
                new RenewDocumentBundleLinkRequest());

        renewed.NotFound.Should().BeTrue();
    }

    // ------------------------------------------------------------ apoyo

    private static CreateDocumentBundleRequest Request(
        params string[] documentIds) =>
        new()
        {
            Title = "Envío pedido 4512",
            Items = documentIds
                .Select(id => new CreateDocumentBundleItem { DocumentId = id })
                .ToList()
        };

    private void GivenDocuments(
        params RenderedDocumentSummary[] summaries)
    {
        _documents
            .Setup(x => x.GetSummariesAsync(
                Tenant,
                It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                (string _, IReadOnlyCollection<string> ids, CancellationToken _) =>
                    summaries
                        .Where(x => ids.Contains(x.Id, StringComparer.OrdinalIgnoreCase))
                        .ToList());
    }

    private static RenderedDocumentSummary Summary(
        string id,
        string fileName,
        string culture) =>
        new()
        {
            Id = id,
            ReportId = "report",
            Culture = culture,
            FileName = fileName,
            CreatedAtUtc = Now
        };

    private static RenderedDocument Rendered(
        string id) =>
        new()
        {
            Id = id,
            ReportId = "albaran",
            Culture = "es-ES",
            FileName = "albaran.html",
            Content = "<html></html>",
            CreatedAtUtc = Now
        };


    private sealed class FixedTimeProvider(DateTime utcNow)
        : TimeProvider
    {
        private DateTimeOffset _now = new(utcNow);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now = _now.Add(by);
    }


    /// <summary>
    /// Repositorio en memoria con la misma semántica que el de EF: el
    /// tenant filtra en gestión y no en el acceso público.
    /// </summary>
    private sealed class InMemoryBundleRepository
        : IDocumentBundleRepository,
          IEnumerable<DocumentBundle>
    {
        private readonly Dictionary<Guid, DocumentBundle> _items = new();

        public int FindCalls { get; private set; }

        public Task CreateAsync(
            DocumentBundle bundle,
            CancellationToken cancellationToken = default)
        {
            _items[bundle.Id] = bundle;
            return Task.CompletedTask;
        }

        public Task<DocumentBundle?> GetAsync(
            string tenantId,
            Guid bundleId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                _items.TryGetValue(bundleId, out var bundle) &&
                bundle.TenantId == tenantId
                    ? bundle
                    : null);

        public Task<DocumentBundle?> FindAsync(
            Guid bundleId,
            CancellationToken cancellationToken = default)
        {
            FindCalls++;

            return Task.FromResult(
                _items.TryGetValue(bundleId, out var bundle)
                    ? bundle
                    : null);
        }

        public Task<IReadOnlyList<DocumentBundle>> ListAsync(
            string tenantId,
            int take,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DocumentBundle>>(
                _items.Values
                    .Where(x => x.TenantId == tenantId)
                    .Take(take)
                    .ToList());

        public Task<bool> RevokeAsync(
            string tenantId,
            Guid bundleId,
            DateTime revokedAtUtc,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                Update(
                    tenantId,
                    bundleId,
                    x => Copy(x, revokedAtUtc: x.RevokedAtUtc ?? revokedAtUtc)));

        public Task<bool> ReplaceLinkAsync(
            string tenantId,
            Guid bundleId,
            byte[] tokenHash,
            DateTime issuedAtUtc,
            DateTime expiresAtUtc,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                Update(
                    tenantId,
                    bundleId,
                    x => Copy(
                        x,
                        tokenHash: tokenHash,
                        issuedAtUtc: issuedAtUtc,
                        expiresAtUtc: expiresAtUtc,
                        clearRevocation: true)));

        public Task RecordAccessAsync(
            DocumentBundleAccess access,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public IEnumerator<DocumentBundle> GetEnumerator() =>
            _items.Values.GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
            GetEnumerator();

        private bool Update(
            string tenantId,
            Guid bundleId,
            Func<DocumentBundle, DocumentBundle> change)
        {
            if (!_items.TryGetValue(bundleId, out var bundle) ||
                bundle.TenantId != tenantId)
            {
                return false;
            }

            _items[bundleId] = change(bundle);
            return true;
        }

        private static DocumentBundle Copy(
            DocumentBundle source,
            byte[]? tokenHash = null,
            DateTime? issuedAtUtc = null,
            DateTime? expiresAtUtc = null,
            DateTime? revokedAtUtc = null,
            bool clearRevocation = false) =>
            new()
            {
                Id = source.Id,
                TenantId = source.TenantId,
                Title = source.Title,
                Message = source.Message,
                Culture = source.Culture,
                TokenHash = tokenHash ?? source.TokenHash,
                TokenIssuedAtUtc = issuedAtUtc ?? source.TokenIssuedAtUtc,
                ExpiresAtUtc = expiresAtUtc ?? source.ExpiresAtUtc,
                RevokedAtUtc = clearRevocation
                    ? null
                    : revokedAtUtc ?? source.RevokedAtUtc,
                CreatedAtUtc = source.CreatedAtUtc,
                Items = source.Items
            };
    }
}
