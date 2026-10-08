using System.Text.Json;
using FluentAssertions;
using Renderset.Core.Configurations;
using Renderset.Core.Rendering;
using Renderset.Core.Services;

namespace Renderset.Tests.Core;

public sealed class ReportQrContentTests
{
    private static readonly JsonElement Data =
        JsonDocument.Parse(
            """
            {
              "salidaid": 3116,
              "cliente": { "nombre": "Inter Cars, S.A." },
              "fecha": "2026-10-06"
            }
            """).RootElement.Clone();

    private static readonly ReportDocumentContext Emitted =
        new()
        {
            DocumentId = "f8b36bc2",
            DocumentUrl = "https://renderset.oran.local/documents/f8b36bc2"
        };

    [Fact]
    public void Resolve_ShouldUseTheDocumentLinkByDefault()
    {
        ReportQrContent.Resolve(null, Data, Emitted)
            .Should()
            .Be("https://renderset.oran.local/documents/f8b36bc2");
    }

    [Fact]
    public void Resolve_ShouldFillDataPlaceholdersEscapedInsideAUrl()
    {
        ReportQrContent.Resolve("https://erp/salidas/{salidaid}?c={cliente.nombre}&f={fecha}", Data, Emitted)
            .Should()
            .Be("https://erp/salidas/3116?c=Inter%20Cars%2C%20S.A.&f=2026-10-06");
    }

    [Fact]
    public void Resolve_ShouldKeepPlainTextAsIs()
    {
        ReportQrContent.Resolve("Salida {salidaid} - {cliente.nombre}", Data, Emitted)
            .Should()
            .Be("Salida 3116 - Inter Cars, S.A.");
    }

    [Fact]
    public void Resolve_ShouldGiveNoQrWhenSomethingIsMissing()
    {
        ReportQrContent.Resolve("{documentUrl}", Data, new ReportDocumentContext { DocumentId = "x" })
            .Should()
            .BeNull();

        ReportQrContent.Resolve("https://erp/{matricula}", Data, Emitted)
            .Should()
            .BeNull();
    }

    [Fact]
    public void Resolve_ShouldShowASampleInThePreview()
    {
        ReportQrContent.Resolve(null, Data, ReportDocumentContext.Preview)
            .Should()
            .NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void DataTokens_ShouldIgnoreDocumentPlaceholders()
    {
        ReportQrContent.DataTokens("{documentUrl}?s={salidaid}&id={documentId}")
            .Should()
            .Equal("salidaid");
    }

    [Theory]
    [InlineData("https://renderset.oran.local/", "https://renderset.oran.local/documents/abc")]
    [InlineData("https://localhost:7221", "https://localhost:7221/documents/abc")]
    [InlineData("ftp://x", null)]
    [InlineData(null, null)]
    public void DocumentUrl_ShouldOnlyBuildHttpLinks(
        string? baseUrl,
        string? expected)
    {
        new DocumentLinkOptions { BaseUrl = baseUrl }
            .DocumentUrl("abc")
            .Should()
            .Be(expected);
    }

    [Fact]
    public void Resolve_ShouldKeepQrOffUnlessConfiguredAndClampItsSize()
    {
        var configuration = ReportSectionGridTests.Configuration();

        new ReportConfigurationResolver()
            .Resolve(ReportSectionGridTests.Definition(), configuration)
            .Header!.ShowQr
            .Should()
            .BeFalse();

        configuration.Header = new ReportHeaderConfiguration
        {
            ShowQr = true,
            QrSize = 999
        };

        var header =
            new ReportConfigurationResolver()
                .Resolve(ReportSectionGridTests.Definition(), configuration)
                .Header!;

        header.ShowQr.Should().BeTrue();
        header.QrContent.Should().Be(ReportQrContent.DocumentUrlTemplate);
        header.QrSize.Should().Be(160);
    }
}
