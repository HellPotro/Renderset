using FluentAssertions;
using Renderset.Core.Rendering;

namespace Renderset.Tests.Core;

public sealed class DocumentEmbeddedDataTests
{
    private const string Document =
        """
        <!DOCTYPE html>
        <html lang="es">
        <body class="report-document-page">
        <div class="report-document-sheet">
        <table><tr><td>00119012.A2</td></tr></table>
        <script type="application/json" class="dynamic-report-data" data-table="jaulas.lineas.table">{"id":"jaulas.lineas.table","rows":[{"Artículo":"00119012.A2"}]}</script>
        </div>
        <script type="application/json" id="rs-document-data">
        {"salidaid":3116,"nota":"<\/script>"}
        </script>
        </body>
        </html>
        """;

    [Fact]
    public void Strip_ShouldRemoveDocumentAndTableDataButKeepWhatIsShown()
    {
        DocumentEmbeddedData.HasData(Document).Should().BeTrue();

        var stripped = DocumentEmbeddedData.Strip(Document);

        stripped.Should().NotContain("rs-document-data");
        stripped.Should().NotContain("dynamic-report-data");
        stripped.Should().NotContain("3116");
        stripped.Should().Contain("<td>00119012.A2</td>");
        stripped.Should().EndWith("</html>");

        DocumentEmbeddedData.HasData(stripped).Should().BeFalse();
    }

    [Fact]
    public void Strip_ShouldLeaveADocumentWithoutDataUntouched()
    {
        const string html = "<html><body><p>Hola</p></body></html>";

        DocumentEmbeddedData.Strip(html).Should().Be(html);
    }
}
