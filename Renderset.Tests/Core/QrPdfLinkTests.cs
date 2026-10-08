using System.Text.Json;
using FluentAssertions;
using Renderset.Core.Rendering;

namespace Renderset.Tests.Core;

public sealed class QrPdfLinkTests
{
    private static DocumentLinkOptions Links() =>
        new DocumentLinkOptions
        {
            BaseUrl = "https://app.renderset.app",
            PublicBaseUrl = "https://docs.renderset.app/"
        }
        .UseSigningSecret("una-clave-de-servicio-de-al-menos-32-caracteres");

    [Fact]
    public void PdfUrl_ShouldBeThePublicLinkEndingInPdf()
    {
        var links = Links();

        links.PdfUrl("oranauto", "abc")
            .Should()
            .Be(links.PublicDocumentUrl("oranauto", "abc") + "/pdf");
    }

    [Fact]
    public void PdfUrl_ShouldFallBackToTheViewerWithoutPublicLinks()
    {
        var links = Links();
        links.Public = false;

        links.PdfUrl("oranauto", "abc")
            .Should()
            .Be("https://app.renderset.app/documents/abc");
    }

    [Fact]
    public void Resolve_ShouldPutThePdfLinkInTheQr()
    {
        var context =
            new ReportDocumentContext
            {
                DocumentId = "abc",
                DocumentUrl = "https://docs.renderset.app/d/oranauto/abc/firma",
                PdfUrl = "https://docs.renderset.app/d/oranauto/abc/firma/pdf"
            };

        ReportQrContent.Resolve(ReportQrContent.PdfUrlTemplate, default, context)
            .Should()
            .Be("https://docs.renderset.app/d/oranauto/abc/firma/pdf");
    }

    [Fact]
    public void DataTokens_ShouldNotTreatPdfUrlAsData()
    {
        ReportQrContent.DataTokens("{pdfUrl}?ref={salidaid}")
            .Should()
            .Equal("salidaid");
    }

    [Fact]
    public void Resolve_ShouldGiveNoQrWhenThePdfLinkIsMissing()
    {
        ReportQrContent.Resolve(
                ReportQrContent.PdfUrlTemplate,
                JsonDocument.Parse("{}").RootElement,
                new ReportDocumentContext { DocumentId = "abc" })
            .Should()
            .BeNull();
    }
}
