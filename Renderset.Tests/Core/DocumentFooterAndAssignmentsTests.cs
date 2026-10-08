using System.Text.Json;
using FluentAssertions;
using Moq;
using Renderset.Core.Blocks;
using Renderset.Core.Configurations;
using Renderset.Core.Definitions;
using Renderset.Core.Localization;
using Renderset.Core.Persistence;
using Renderset.Core.Presets;
using Renderset.Core.Rendering;
using Renderset.Core.Rendering.Pdf;
using Renderset.Core.Reports;
using Renderset.Core.Services;
using Renderset.Core.Themes;

namespace Renderset.Tests.Core;

public sealed class ReportDataTemplateTests
{
    private static readonly JsonElement Data =
        JsonDocument.Parse(
            """
            {
              "numero": 3116,
              "fecha": "2026-10-06",
              "cliente": { "codigo": "000762", "nombre": "Talleres Peláez" }
            }
            """).RootElement;

    [Fact]
    public void Apply_ShouldReplaceDataTokensAndKeepTheRest()
    {
        ReportDataTemplate.Apply("Albarán {{data.numero}} · {{data.cliente.nombre}} · {{company.cif}}", Data)
            .Should()
            .Be("Albarán 3116 · Talleres Peláez · {{company.cif}}");
    }

    [Fact]
    public void Apply_ShouldKeepLeadingZerosAndLeaveMissingDataBlank()
    {
        ReportDataTemplate.Apply("{{data.cliente.codigo}}/{{data.noexiste}}", Data)
            .Should()
            .Be("000762/");
    }

    [Fact]
    public void Apply_ShouldLetTheCallerMarkMissingData()
    {
        ReportDataTemplate.Apply("Nº {{data.numero}}", default, path => $"<{path}>")
            .Should()
            .Be("Nº <numero>");
    }
}


public sealed class ReportPageFooterTests
{
    private static readonly ReportPageFooter.Content Content =
        new("Inscrita en el R.M. de Cantabria\n\"ORAN\" <S.L.U.>", "Generado el 07/10/2026 10:00", "Página {page} de {pages}");

    [Fact]
    public void Template_ShouldRoundTripThroughTheEmittedDocument()
    {
        var html =
            "<html><head><style>body{}</style>" +
            ReportPageFooter.BuildPrintStyle(Content, new ReportTheme()) +
            "</head><body>doc" +
            ReportPageFooter.BuildTemplate(Content, new ReportTheme()) +
            "</body></html>";

        var extracted = ReportPageFooter.Extract(html);

        extracted.Should().NotBeNull();
        extracted!.Html.Should().NotContain("rs-page-footer").And.NotContain("rs-page-print");
        extracted.Html.Should().Contain("<style>body{}</style>").And.Contain("doc");
        extracted.FooterHtml.Should().Contain("<span class=\"pageNumber\"></span>");
        extracted.FooterHtml.Should().Contain("<span class=\"totalPages\"></span>");
        extracted.FooterHtml.Should().Contain("&lt;S.L.U.&gt;");
        extracted.HeightMm.Should().BeGreaterOrEqualTo(ReportPageFooter.MinBottomMarginMm);
    }

    [Fact]
    public void PrintStyle_ShouldNotLetTheTextCloseTheStyleBlock()
    {
        var css =
            ReportPageFooter.BuildPrintStyle(
                new ReportPageFooter.Content("</style><script>x</script>", null, null),
                null);

        css.Should().NotContain("</style><script>");
        css.Should().NotContain("counter(page)", "sin numeración no se pinta");
    }

    [Fact]
    public void Extract_ShouldReturnNullForDocumentsWithoutFooter()
    {
        ReportPageFooter.Extract("<html>factura</html>").Should().BeNull();
    }

    [Fact]
    public async Task Pdf_ShouldUseTheDocumentFooterInsteadOfTheGenericNumbering()
    {
        var documents = new Mock<IRenderedDocumentRepository>();
        var converter = new Mock<IPdfConverter>();
        var options = new PdfOptions();

        var html =
            "<html><body>doc" +
            ReportPageFooter.BuildTemplate(Content, null) +
            "</body></html>";

        documents
            .Setup(x => x.GetByIdAsync("t", "doc", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RenderedDocument
            {
                Id = "doc",
                ReportId = "factura",
                Culture = "es-ES",
                FileName = "factura.html",
                Content = html
            });

        PdfPageOptions? used = null;
        string? sent = null;

        converter
            .Setup(x => x.ConvertHtmlAsync(It.IsAny<string>(), It.IsAny<PdfPageOptions>(), It.IsAny<CancellationToken>()))
            .Callback<string, PdfPageOptions, CancellationToken>((h, p, _) =>
            {
                sent = h;
                used = p;
            })
            .ReturnsAsync(new byte[] { 1 });

        await new DocumentPdfService(documents.Object, converter.Object, options)
            .GetOrCreateAsync("t", "doc");

        sent.Should().NotContain("rs-page-footer");
        used!.FooterHtml.Should().Contain("pageNumber");
        used.ShowPageNumbers.Should().BeFalse();
        used.MarginBottomMm.Should().BeGreaterOrEqualTo(options.Page.MarginBottomMm);
    }
}


public sealed class FieldsBlockResolverTests
{
    [Fact]
    public void Resolve_ShouldTranslateFixedFieldsAndKeepDataTokens()
    {
        var definition = new ReportDefinition
        {
            Id = "albaran",
            Name = "Albarán",
            Sections = []
        };

        var block = new ReportBlock
        {
            Id = "empresa",
            Name = "Datos de empresa",
            Type = ReportBlockType.Fields,
            ConfigurationJson = JsonSerializer.Serialize(
                new ReportFieldsBlockConfiguration
                {
                    TitleKey = "block.empresa.title",
                    Layout = ReportSectionLayout.Grid,
                    Columns = 3,
                    Fields =
                    [
                        new ReportFixedFieldConfiguration { Id = "f1", Order = 10 },
                        new ReportFixedFieldConfiguration { Id = "f2", Order = 20 },
                        new ReportFixedFieldConfiguration { Id = "vacio", Order = 30 }
                    ]
                },
                new JsonSerializerOptions(JsonSerializerDefaults.Web))
        };

        var configuration = new ReportConfiguration
        {
            Id = "p",
            ReportId = "albaran",
            Name = "p",
            Body =
            [
                new ReportBodyItemConfiguration
                {
                    Id = "b1",
                    Type = ReportBodyItemType.Block,
                    BlockId = "empresa",
                    Order = 10
                }
            ]
        };

        var catalog = new ReportTextCatalog(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["block.empresa.title"] = "Empresa",
                [ReportTextKeys.BlockFieldLabel("f1")] = "CIF",
                [ReportTextKeys.BlockFieldValue("f1")] = "B39000000",
                [ReportTextKeys.BlockFieldLabel("f2")] = "Pedido",
                [ReportTextKeys.BlockFieldValue("f2")] = "{{data.pedido}}"
            });

        var resolved =
            new ReportConfigurationResolver().Resolve(
                definition,
                configuration,
                [block],
                catalog);

        var fields = resolved.Body.Single().Block!.Fields!;

        fields.Title.Should().Be("Empresa");
        fields.Layout.Should().Be(ReportSectionLayout.Grid);
        fields.Columns.Should().Be(3);
        fields.Fields.Select(x => x.Label).Should().Equal("CIF", "Pedido");
        fields.Fields[1].Value.Should().Be("{{data.pedido}}");
    }
}


public sealed class PresetAssignmentTests
{
    [Fact]
    public void Csv_ShouldRoundTripAndKeepCodesAsText()
    {
        var csv =
            ReportPresetAssignmentCsv.Build(
                "cliente.codigo",
                "albaran-pelaez",
                [],
                emptyRows: 2);

        csv = csv.Replace("cliente.codigo;;albaran-pelaez", "cliente.codigo;000762;albaran-pelaez");

        var parsed = ReportPresetAssignmentCsv.Parse(csv);

        parsed.Errors.Should().BeEmpty();
        parsed.Rows.Should().ContainSingle();
        parsed.Rows[0].ContextKey.Should().Be("000762");
        parsed.Rows[0].PresetId.Should().Be("albaran-pelaez");
    }

    [Fact]
    public void Csv_ShouldReportDuplicatedContexts()
    {
        var parsed =
            ReportPresetAssignmentCsv.Parse(
                "tipo_contexto,valor,preset\ncustomer,1,a\ncustomer,1,b\n");

        parsed.Errors.Should().ContainSingle(x => x.Contains("Repetido"));
    }

    [Fact]
    public async Task Provider_ShouldPickThePresetFromTheDocumentData()
    {
        var presets = new Mock<IReportPresetRepository>();
        var assignments = new Mock<IReportPresetAssignmentRepository>();

        assignments
            .Setup(x => x.GetAllAsync("t", "albaran", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new List<ReportPresetAssignment>
                {
                    new() { ReportId = "albaran", PresetId = "default" },
                    new() { ReportId = "albaran", ContextType = "cliente.codigo", ContextKey = "000815", PresetId = "otro" },
                    new() { ReportId = "albaran", ContextType = "cliente.codigo", ContextKey = "000762", PresetId = "pelaez" }
                });

        presets
            .Setup(x => x.GetByIdAsync("t", "pelaez", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Preset("pelaez"));

        var data =
            JsonDocument.Parse("""{ "cliente": { "codigo": "000762" } }""").RootElement;

        var result =
            await new ReportPresetProvider(presets.Object, assignments.Object)
                .GetPresetAsync("t", "albaran", null, null, data);

        result!.Id.Should().Be("pelaez");

        assignments.Verify(
            x => x.GetDefaultAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void SingleValuedFields_ShouldSkipRepeatedSections()
    {
        var definition = new ReportDefinition
        {
            Id = "factura",
            Name = "Factura",
            Sections =
            [
                new ReportSectionDefinition
                {
                    Id = "general",
                    Name = "General",
                    Fields = [new ReportFieldDefinition { Id = "numero", Label = "Número", DataPath = "numero" }]
                },
                new ReportSectionDefinition
                {
                    Id = "albaranes",
                    Name = "Albaranes",
                    DataPath = "albaranes",
                    Fields = [new ReportFieldDefinition { Id = "alb", Label = "Albarán", DataPath = "numero" }]
                }
            ]
        };

        ReportDataFields.SingleValued(definition)
            .Select(x => x.Label)
            .Should()
            .Equal("Número");
    }

    private static ReportPreset Preset(string id) =>
        new()
        {
            Id = id,
            Configuration = new ReportConfiguration
            {
                Id = id,
                ReportId = "albaran",
                Name = id
            },
            Theme = new ReportTheme()
        };
}


public sealed class PublicDocumentLinkTests
{
    private static DocumentLinkOptions Links() =>
        new DocumentLinkOptions
        {
            BaseUrl = "https://app.renderset.app",
            PublicBaseUrl = "https://docs.renderset.app/"
        }
        .UseSigningSecret("una-clave-de-servicio-de-al-menos-32-caracteres");

    [Fact]
    public void DocumentUrl_ShouldBeThePublicSignedLink()
    {
        var links = Links();

        var url = links.DocumentUrl("oranauto", "89ef5054650d4db191fc606f04aa3b40");

        url.Should().StartWith("https://docs.renderset.app/d/oranauto/89ef5054650d4db191fc606f04aa3b40/");

        var signature = url![(url.LastIndexOf('/') + 1)..];

        signature.Should().HaveLength(22);
        links.Verify("oranauto", "89ef5054650d4db191fc606f04aa3b40", signature).Should().BeTrue();
        links.Verify("otro", "89ef5054650d4db191fc606f04aa3b40", signature).Should().BeFalse();
        links.Verify("oranauto", "00000000000000000000000000000000", signature).Should().BeFalse();
    }

    [Fact]
    public void DocumentUrl_ShouldFallBackToTheViewerWithoutKeyOrWhenDisabled()
    {
        new DocumentLinkOptions { BaseUrl = "https://app.renderset.app", PublicBaseUrl = "https://docs.renderset.app" }
            .DocumentUrl("oranauto", "abc")
            .Should()
            .Be("https://app.renderset.app/documents/abc");

        var disabled = Links();
        disabled.Public = false;

        disabled.DocumentUrl("oranauto", "abc")
            .Should()
            .Be("https://app.renderset.app/documents/abc");
    }
}


public sealed class ReportResourcePromotionTests
{
    private const string Custom = "section.custom-0d70";

    private static readonly ReportDefinition Definition = new()
    {
        Id = "deca-2026",
        Name = "DECA",
        Sections =
        [
            new ReportSectionDefinition
            {
                Id = "general",
                Name = "General",
                Fields = [new ReportFieldDefinition { Id = "numero", Label = "Número", DataPath = "numero" }]
            }
        ]
    };

    private static ReportResourcePromotion.PresetInfo Preset(string id, bool withCustom) =>
        new(
            id,
            id,
            new ReportConfiguration
            {
                Id = id,
                ReportId = "deca-2026",
                Name = id,
                Sections = withCustom
                    ? [new ReportSectionConfiguration { SectionId = "custom-0d70" }]
                    : []
            });

    private static ReportResource Text(string presetId, string key, string culture, string value) =>
        new()
        {
            Scope = ReportResourceScope.ForPreset(presetId),
            Key = key,
            Culture = culture,
            Value = value
        };

    [Fact]
    public void Build_ShouldMoveTheRepeatedCustomSectionNameToTheReport()
    {
        var plan =
            ReportResourcePromotion.Build(
                Definition,
                [Preset("default", true), Preset("personalizado", true), Preset("otro", false)],
                [],
                new Dictionary<string, IReadOnlyCollection<ReportResource>>
                {
                    ["default"] = [Text("default", Custom, "es-ES", "Destino")],
                    ["personalizado"] =
                    [
                        Text("personalizado", Custom, "es-ES", "Destino"),
                        Text("personalizado", Custom, "pt-PT", "Destino")
                    ],
                    ["otro"] = []
                });

        plan.PromotedKeys.Should().Equal(Custom);
        plan.ReportWrites.Select(x => (x.Scope, x.Culture)).Should().BeEquivalentTo(
            new[] { ("deca-2026", "es-ES"), ("deca-2026", "pt-PT") });
        plan.Deletions.Should().HaveCount(2);
    }

    [Fact]
    public void Build_ShouldKeepRealOverridesAndKeysOtherPresetsWouldInherit()
    {
        var plan =
            ReportResourcePromotion.Build(
                Definition,
                [Preset("a", false), Preset("b", false), Preset("c", false)],
                [],
                new Dictionary<string, IReadOnlyCollection<ReportResource>>
                {
                    // Igual en a y b, pero c heredaría "Nº albarán" en vez de "Número".
                    ["a"] = [Text("a", "field.numero", "es-ES", "Nº albarán"), Text("a", "section.general", "es-ES", "Datos")],
                    ["b"] = [Text("b", "field.numero", "es-ES", "Nº albarán"), Text("b", "section.general", "es-ES", "Cabecera")],
                    ["c"] = []
                });

        plan.IsEmpty.Should().BeTrue();
        plan.SkippedKeys.Select(x => x.Key).Should().BeEquivalentTo("field.numero", "section.general");
    }
}
