using System.Security.Cryptography;
using System.Text.Json;
using FluentAssertions;
using Moq;
using Renderset.Core.Rendering;
using Renderset.Core.Rendering.Pdf;
using Renderset.Core.Resolved;
using Renderset.Core.Sharing;
using Renderset.Core.Themes;
using Renderset.Deca;
using Renderset.Deca.Issuing;

namespace Renderset.Tests.Deca;

public sealed class DecaIssuerTests
{
    private const string Tenant = "oranauto";

    private readonly Mock<IDecaRepository> _decas = new();
    private readonly Mock<IReportDocumentRenderer> _renderer = new();
    private readonly Mock<IPdfConverter> _pdf = new();
    private readonly Mock<IDocumentSharingSettingsRepository> _sharing = new();

    private readonly DecaOptions _options =
        new() { PublicBaseUrl = "https://deca.renderset.app" };

    private ReportDocumentContext? _context;
    private JsonElement _data;
    private PdfPageOptions? _page;

    private static readonly byte[] Pdf = "%PDF-1.7 deca"u8.ToArray();

    public DecaIssuerTests()
    {
        _decas
            .Setup(x => x.NextSequenceAsync(Tenant, 2026, It.IsAny<CancellationToken>()))
            .ReturnsAsync(7);

        _renderer
            .Setup(x => x.RenderHtmlAsync(
                It.IsAny<ResolvedReportDefinition>(),
                It.IsAny<JsonElement>(),
                It.IsAny<ReportTheme>(),
                It.IsAny<string?>(),
                It.IsAny<bool>(),
                It.IsAny<ReportDocumentContext?>(),
                It.IsAny<CancellationToken>()))
            .Callback((ResolvedReportDefinition _, JsonElement data, ReportTheme _, string? _, bool _, ReportDocumentContext? context, CancellationToken _) =>
            {
                _data = data.Clone();
                _context = context;
            })
            .ReturnsAsync("<html><body>DeCA</body></html>");

        _pdf.SetupGet(x => x.IsAvailable).Returns(true);

        _sharing
            .Setup(x => x.GetAsync(Tenant, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentSharingSettings { TenantName = "Talleres Oran" });

        _pdf
            .Setup(x => x.ConvertHtmlAsync(It.IsAny<string>(), It.IsAny<PdfPageOptions>(), It.IsAny<CancellationToken>()))
            .Callback((string _, PdfPageOptions page, CancellationToken _) => _page = page)
            .ReturnsAsync(Pdf);
    }

    /// <summary>
    /// 8 de octubre de 2026, 10:00 en Madrid.
    /// </summary>
    private DecaIssuer Issuer() =>
        new(
            _decas.Object,
            _renderer.Object,
            _pdf.Object,
            new PdfOptions(),
            _sharing.Object,
            _options,
            new FixedTime(new DateTimeOffset(2026, 10, 8, 8, 0, 0, TimeSpan.Zero)));

    [Fact]
    public async Task Issue_ShouldFreezeThePdfAndSaveEverything()
    {
        var result = await Issuer().IssueAsync(Tenant, DecaValidatorTests.Valid(), "alberto");

        result.Succeeded.Should().BeTrue();

        var deca = result.Deca!;

        deca.Number.Should().Be("DECA-2026-000007");
        deca.IssuedAtUtc.Should().Be(new DateTime(2026, 10, 8, 8, 0, 0, DateTimeKind.Utc));
        deca.RetainUntilUtc.Should().Be(deca.IssuedAtUtc.AddDays(365));
        deca.PublicUntilUtc.Should().BeNull();
        DecaPublicCode.IsWellFormed(deca.PublicCode).Should().BeTrue();

        deca.Versions.Should().ContainSingle();
        deca.Versions[0].Sha256.Should().Be(Convert.ToHexString(SHA256.HashData(Pdf)).ToLowerInvariant());
        deca.Versions[0].SizeBytes.Should().Be(Pdf.Length);
        deca.Events.Should().ContainSingle(x => x.Kind == DecaEventKind.Issued && x.Actor == "alberto");

        // El QR lleva el enlace corto que descarga el PDF.
        var url = $"https://deca.renderset.app/q/{deca.PublicCode}";

        _context!.PdfUrl.Should().Be(url);
        _context.DocumentId.Should().Be(deca.Id);

        // El DeCA se guarda con su PDF, en su propia base de datos.
        _decas.Verify(x => x.AddAsync(deca, Pdf, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Issue_ShouldWriteCreationAndModificationDatesInSpanishTime()
    {
        await Issuer().IssueAsync(Tenant, DecaValidatorTests.Valid(), null);

        _page!.Metadata.Should().NotBeNull();
        _page.Metadata!["CreationDate"].Should().Be("2026-10-08T10:00:00+02:00");
        _page.Metadata["ModDate"].Should().Be("2026-10-08T10:00:00+02:00");
        _page.Metadata["Title"].Should().Be("DeCA DECA-2026-000007");
    }

    [Fact]
    public async Task Issue_ShouldRecordTheDesignVersionUsed()
    {
        var templates = new Mock<IDecaTemplateRepository>();

        templates
            .Setup(x => x.GetCurrentAsync(Tenant, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DecaTemplate
            {
                TenantId = Tenant,
                Version = 3,
                Configuration = DecaReportTemplate.Configuration(branding: null)
            });

        var issuer =
            new DecaIssuer(
                _decas.Object,
                _renderer.Object,
                _pdf.Object,
                new PdfOptions(),
                _sharing.Object,
                _options,
                new FixedTime(new DateTimeOffset(2026, 10, 8, 8, 0, 0, TimeSpan.Zero)),
                assetInliner: null,
                templates: templates.Object);

        var result = await issuer.IssueAsync(Tenant, DecaValidatorTests.Valid(), null);

        result.Deca!.Versions[0].TemplateVersion.Should().Be(3);
        result.Deca.Events[0].Detail.Should().Contain("diseño v3");
    }

    [Fact]
    public async Task Issue_ShouldNotSaveAnythingWhenTheDataIsInvalid()
    {
        var data = DecaValidatorTests.Valid();
        data.Shipper.TaxId = "B12345675";

        var result = await Issuer().IssueAsync(Tenant, data, null);

        result.Succeeded.Should().BeFalse();
        result.ToApiErrors().Should().ContainSingle(x => x.Path == "shipper.taxId");

        VerifyNothingSaved();
        _decas.Verify(x => x.NextSequenceAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Issue_ShouldNotIssueWithoutPdf()
    {
        _pdf
            .Setup(x => x.ConvertHtmlAsync(It.IsAny<string>(), It.IsAny<PdfPageOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PdfConversionException("Gotenberg caído"));

        var result = await Issuer().IssueAsync(Tenant, DecaValidatorTests.Valid(), null);

        result.Succeeded.Should().BeFalse();
        result.Failure!.Code.Should().Be("deca.pdf_failed");

        VerifyNothingSaved();
    }

    [Fact]
    public async Task Issue_ShouldRejectPdfsOverFiveMegabytes()
    {
        _pdf
            .Setup(x => x.ConvertHtmlAsync(It.IsAny<string>(), It.IsAny<PdfPageOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new byte[DecaOptions.MaxPdfBytes + 1]);

        var result = await Issuer().IssueAsync(Tenant, DecaValidatorTests.Valid(), null);

        result.Failure!.Code.Should().Be("deca.pdf_too_large");

        VerifyNothingSaved();
    }

    [Fact]
    public async Task Issue_ShouldRefuseWithoutAnHttpsDomainForTheQr()
    {
        _options.PublicBaseUrl = "http://deca.renderset.app";

        var result = await Issuer().IssueAsync(Tenant, DecaValidatorTests.Valid(), null);

        result.Failure!.Code.Should().Be("deca.public_url_missing");

        VerifyNothingSaved();
    }

    [Fact]
    public async Task Issue_ShouldStopForAnUnknownTenantBeforeThePdf()
    {
        var result = await Issuer().IssueAsync("no-existe", DecaValidatorTests.Valid(), null);

        result.Failure!.Code.Should().Be("deca.tenant_not_found");

        _pdf.Verify(x => x.ConvertHtmlAsync(It.IsAny<string>(), It.IsAny<PdfPageOptions>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifyNothingSaved();
    }

    [Fact]
    public async Task Issue_ShouldAnswerNullSectionsWithValidationErrors()
    {
        // Lo que puede llegar por la API: el JSON no respeta la nulabilidad.
        var data = new DecaData { Shipper = null!, Vehicle = null!, Shipments = null! };

        var result = await Issuer().IssueAsync(Tenant, data, null);

        result.Succeeded.Should().BeFalse();
        result.Failure.Should().BeNull();
        result.ToApiErrors().Select(x => x.Path).Should().Contain("shipper.name");
    }

    [Fact]
    public async Task Template_ShouldFindEveryFieldInTheData()
    {
        await Issuer().IssueAsync(Tenant, DecaValidatorTests.Valid(), null);

        var definition = DecaReportTemplate.Definition;

        foreach (var field in definition.Sections.SelectMany(x => x.Fields))
            Resolve(_data, field.DataPath).Should().NotBeNull($"la plantilla pinta {field.DataPath}");

        var shipments = _data.GetProperty("envios");

        shipments.GetArrayLength().Should().Be(1);

        foreach (var column in definition.Sections.Single(x => x.Table is not null).Table!.Columns)
            shipments[0].TryGetProperty(column.DataPath, out _).Should().BeTrue(column.DataPath);

        shipments[0].GetProperty("cantidad").GetString().Should().Be("12.500 kg");
    }

    [Fact]
    public void Quantity_ShouldShowWeightAndAlternative()
    {
        DecaReportData.Quantity(new DecaShipment { WeightKg = 1234.5m })
            .Should().Be("1.234,5 kg");

        DecaReportData.Quantity(new DecaShipment { AlternativeQuantity = 33, AlternativeUnit = "m³" })
            .Should().Be("33 m³");

        DecaReportData.Quantity(new DecaShipment())
            .Should().Be(DecaReportData.NotApplicable);
    }

    private void VerifyNothingSaved() =>
        _decas.Verify(x => x.AddAsync(It.IsAny<DecaDocument>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);

    private static string? Resolve(
        JsonElement data,
        string path)
    {
        var current = data;

        foreach (var part in path.Split('.'))
        {
            if (current.ValueKind != JsonValueKind.Object ||
                !current.TryGetProperty(part, out current))
            {
                return null;
            }
        }

        return current.ValueKind == JsonValueKind.String
            ? current.GetString()
            : null;
    }

    private sealed class FixedTime(
        DateTimeOffset now)
        : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
