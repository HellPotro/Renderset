using FluentAssertions;
using Moq;
using Renderset.Core.Rendering;
using Renderset.Core.Rendering.Pdf;

namespace Renderset.Tests.Core;

public sealed class DocumentPdfServiceTests
{
    private const string Tenant = "oranauto";

    private readonly Mock<IRenderedDocumentRepository> _documents = new();
    private readonly Mock<IPdfConverter> _converter = new();
    private readonly PdfOptions _options = new();

    public DocumentPdfServiceTests()
    {
        _converter.SetupGet(x => x.IsAvailable).Returns(true);
    }

    private DocumentPdfService CreateSut() =>
        new(_documents.Object, _converter.Object, _options);

    [Fact]
    public async Task GetOrCreateAsync_ShouldServeTheStoredPdfWithoutConverting()
    {
        _documents
            .Setup(x => x.GetPdfAsync(Tenant, "doc", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new byte[] { 1, 2, 3 });

        var pdf = await CreateSut().GetOrCreateAsync(Tenant, "doc");

        pdf.Should().Equal(1, 2, 3);

        _converter.Verify(
            x => x.ConvertHtmlAsync(
                It.IsAny<string>(),
                It.IsAny<PdfPageOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetOrCreateAsync_ShouldConvertTheEmittedHtmlAndKeepIt()
    {
        _documents
            .Setup(x => x.GetByIdAsync(Tenant, "doc", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Document("doc", "<html>factura</html>"));

        _converter
            .Setup(x => x.ConvertHtmlAsync(
                "<html>factura</html>",
                _options.Page,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new byte[] { 9, 9 });

        var pdf = await CreateSut().GetOrCreateAsync(Tenant, "doc");

        pdf.Should().Equal(9, 9);

        _documents.Verify(
            x => x.SavePdfAsync(
                Tenant,
                "doc",
                It.Is<byte[]>(b => b.SequenceEqual(new byte[] { 9, 9 })),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetOrCreateAsync_ShouldReturnNullForUnknownDocuments()
    {
        var pdf = await CreateSut().GetOrCreateAsync(Tenant, "no-existe");

        pdf.Should().BeNull();
    }

    [Fact]
    public async Task MergeAsync_ShouldKeepTheRequestedOrderAndSkipMissingDocuments()
    {
        _documents
            .Setup(x => x.GetPdfAsync(Tenant, "a", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new byte[] { 1 });

        _documents
            .Setup(x => x.GetPdfAsync(Tenant, "b", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new byte[] { 2 });

        IReadOnlyList<byte[]>? merged = null;

        _converter
            .Setup(x => x.MergeAsync(
                It.IsAny<IReadOnlyList<byte[]>>(),
                It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<byte[]>, CancellationToken>((pdfs, _) => merged = pdfs)
            .ReturnsAsync(new byte[] { 7 });

        var pdf =
            await CreateSut().MergeAsync(
                Tenant,
                ["b", "no-existe", "a"]);

        pdf.Should().Equal(7);
        merged!.Select(x => x[0]).Should().Equal(2, 1);
    }

    [Fact]
    public async Task MergeAsync_ShouldNotCallTheConverterForASingleDocument()
    {
        _documents
            .Setup(x => x.GetPdfAsync(Tenant, "a", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new byte[] { 1 });

        var pdf = await CreateSut().MergeAsync(Tenant, ["a"]);

        pdf.Should().Equal(1);

        _converter.Verify(
            x => x.MergeAsync(
                It.IsAny<IReadOnlyList<byte[]>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static RenderedDocument Document(
        string id,
        string html) =>
        new()
        {
            Id = id,
            ReportId = "factura",
            Culture = "es-ES",
            FileName = "factura.html",
            Content = html,
            CreatedAtUtc = DateTime.UtcNow
        };
}


public sealed class HtmlImageSourcesTests
{
    [Fact]
    public void FindRemote_ShouldReturnDistinctDecodedUrls()
    {
        const string html =
            """
            <img src="https://oran.es/logo.png?v=1&amp;s=2" alt="Logo" />
            <div><img class="x" src="https://oran.es/logo.png?v=1&amp;s=2"></div>
            <img src="data:image/png;base64,AAAA" />
            <img src="/relativa.png" />
            """;

        HtmlImageSources.FindRemote(html)
            .Should()
            .Equal("https://oran.es/logo.png?v=1&s=2");
    }

    [Fact]
    public void Replace_ShouldSwapOnlyTheUrlsWithReplacement()
    {
        const string html =
            """<img src="https://a.es/1.png" /><img src="https://b.es/2.png" />""";

        var result =
            HtmlImageSources.Replace(
                html,
                new Dictionary<string, string>
                {
                    ["https://a.es/1.png"] = "data:image/png;base64,QUJD"
                });

        result.Should().Be(
            """<img src="data:image/png;base64,QUJD" /><img src="https://b.es/2.png" />""");
    }
}


public sealed class PdfPageOptionsTests
{
    [Theory]
    [InlineData("A4", 210, 297)]
    [InlineData("letter", 215.9, 279.4)]
    [InlineData("otro", 210, 297)]
    public void PaperSizeMm_ShouldMapKnownSizesAndDefaultToA4(
        string paper,
        double width,
        double height)
    {
        new PdfPageOptions { Paper = paper }
            .PaperSizeMm
            .Should()
            .Be((width, height));
    }
}
