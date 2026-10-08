using System.IO.Compression;
using System.Text;
using FluentAssertions;
using Renderset.Core.DataSources;
using Renderset.Core.Reports;

namespace Renderset.Tests.Core;

public sealed class TabularFileTests
{
    [Fact]
    public void Read_Xlsx_ShouldSkipTitleRowsAndKeepTypes()
    {
        var bytes =
            Xlsx(
                sheetXml: """
                <row r="1"><c r="A1" t="s"><v>0</v></c></row>
                <row r="3">
                  <c r="A3" t="s"><v>1</v></c>
                  <c r="B3" t="s"><v>2</v></c>
                  <c r="C3" t="s"><v>3</v></c>
                  <c r="D3" t="s"><v>4</v></c>
                </row>
                <row r="4">
                  <c r="A4"><v>3129</v></c>
                  <c r="B4" t="s"><v>5</v></c>
                  <c r="C4" s="1"><v>46303</v></c>
                  <c r="D4"><v>33.356200000000001</v></c>
                </row>
                <row r="5">
                  <c r="A5"><v>3130</v></c>
                  <c r="B5" t="inlineStr"><is><t>00456</t></is></c>
                  <c r="C5" s="1"><v>46304</v></c>
                  <c r="D5"><v>1.234</v></c>
                </row>
                """,
                sharedStrings: ["Salidas de octubre", "SalidaId", "Cliente", "Fecha", "Precio", "00123"]);

        var result = TabularFile.Read(bytes, "salidas.xlsx");

        result.Format.Should().Be(TabularFileFormat.Excel);
        result.HeaderRow.Should().Be(3);
        result.SkippedRowsAbove.Should().Be(1);
        result.Sheets.Should().Equal("Datos");

        result.Payload.Columns.Select(x => x.Name)
            .Should().Equal("SalidaId", "Cliente", "Fecha", "Precio");

        result.Payload.Columns.Select(x => x.Type)
            .Should().Equal(
                ReportDataType.Number,
                ReportDataType.String,
                ReportDataType.Date,
                ReportDataType.Number);

        result.Payload.Rows.Should().HaveCount(2);

        // Texto con ceros a la izquierda: sigue siendo una referencia.
        result.Payload.Rows[0][1].Should().Be("00123");

        result.Payload.Rows[0][2].Should().Be(new DateTime(2026, 10, 8));

        // 33.356200000000001 del XML vuelve a ser 33.3562, y 1.234 de un
        // Excel no se lee como mil doscientos aunque el servidor esté en
        // español.
        result.Payload.Rows[0][3].Should().Be(33.3562m);
        result.Payload.Rows[1][3].Should().Be(1.234m);
    }

    [Fact]
    public void Read_Xlsx_ShouldHonourExplicitHeaderRow()
    {
        var bytes =
            Xlsx(
                sheetXml: """
                <row r="1"><c r="A1" t="inlineStr"><is><t>A</t></is></c><c r="B1" t="inlineStr"><is><t>B</t></is></c></row>
                <row r="2"><c r="A2" t="inlineStr"><is><t>Codigo</t></is></c><c r="B2" t="inlineStr"><is><t>Nombre</t></is></c></row>
                <row r="3"><c r="A3" t="inlineStr"><is><t>X1</t></is></c><c r="B3" t="inlineStr"><is><t>Uno</t></is></c></row>
                """);

        var result =
            TabularFile.Read(
                bytes,
                "datos.xlsx",
                new TabularFileOptions { HeaderRow = 2 });

        result.HeaderRow.Should().Be(2);
        result.HeaderRowDetected.Should().BeFalse();
        result.Payload.Columns.Select(x => x.Name).Should().Equal("Codigo", "Nombre");
        result.Payload.Rows.Should().ContainSingle();
    }

    [Fact]
    public void Read_Xlsx_ShouldFillGapsBetweenCells()
    {
        var bytes =
            Xlsx(
                sheetXml: """
                <row r="1"><c r="A1" t="inlineStr"><is><t>A</t></is></c><c r="B1" t="inlineStr"><is><t>B</t></is></c><c r="C1" t="inlineStr"><is><t>C</t></is></c></row>
                <row r="2"><c r="A2"><v>1</v></c><c r="C2"><v>3</v></c></row>
                """);

        var result = TabularFile.Read(bytes, "huecos.xlsx");

        result.Payload.Rows[0].Should().Equal(1m, null, 3m);
    }

    [Fact]
    public void Read_ShouldDecodeWindows1252Csv()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        var bytes =
            Encoding.GetEncoding(1252).GetBytes(
                "Albarán;Descripción;Importe\r\n" +
                "001148;Pieza ñ;12,50 €\r\n");

        var result = TabularFile.Read(bytes, "export.csv");

        result.Format.Should().Be(TabularFileFormat.DelimitedText);
        result.Encoding.Should().Be("Windows-1252");
        result.Payload.Columns.Select(x => x.Name).Should().Equal("Albarán", "Descripción", "Importe");
        result.Payload.Rows[0][1].Should().Be("Pieza ñ");
    }

    [Fact]
    public void Read_ShouldReadUtf8CsvWithBom()
    {
        var bytes =
            Encoding.UTF8.GetPreamble()
                .Concat(Encoding.UTF8.GetBytes("Código,Nombre\n1,Señal\n"))
                .ToArray();

        var result = TabularFile.Read(bytes, "utf8.csv");

        result.Encoding.Should().Be("UTF-8");
        result.Payload.Columns[0].Name.Should().Be("Código");
        result.Payload.Rows[0][1].Should().Be("Señal");
    }

    [Fact]
    public void Read_ShouldRejectLegacyXls()
    {
        var bytes = new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 };

        var act = () => TabularFile.Read(bytes, "viejo.xls");

        act.Should().Throw<TabularFileException>()
            .WithMessage("*.xlsx*");
    }

    [Theory]
    [InlineData("dd/mm/yyyy", "Date")]
    [InlineData("dd/mm/yyyy hh:mm", "DateTime")]
    [InlineData("hh:mm", "Time")]
    [InlineData("[h]:mm", "Time")]
    [InlineData("#,##0.00 \"€\"", "Number")]
    [InlineData("0.00E+00", "Number")]
    [InlineData("[Red]#,##0", "Number")]
    [InlineData("[$-es-ES]d \"de\" mmmm \"de\" yyyy", "Date")]
    public void ClassifyCode_ShouldRecogniseDateFormats(
        string code,
        string expected)
    {
        XlsxReader.ClassifyCode(code).ToString().Should().Be(expected);
    }

    [Theory]
    [InlineData("A1", 0)]
    [InlineData("C12", 2)]
    [InlineData("AA3", 26)]
    public void ColumnIndex_ShouldReadLetters(
        string reference,
        int expected)
    {
        XlsxReader.ColumnIndex(reference).Should().Be(expected);
    }

    /// <summary>
    /// Un .xlsx mínimo con una hoja "Datos", como lo guarda Excel: rutas
    /// relativas a xl/, cadenas compartidas y un estilo de fecha (s="1").
    /// </summary>
    private static byte[] Xlsx(
        string sheetXml,
        IReadOnlyList<string>? sharedStrings = null)
    {
        using var stream = new MemoryStream();

        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            void Add(string path, string content)
            {
                using var writer = new StreamWriter(zip.CreateEntry(path).Open(), new UTF8Encoding(false));
                writer.Write(content);
            }

            Add("[Content_Types].xml", """<?xml version="1.0" encoding="UTF-8"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"/>""");

            Add("xl/workbook.xml", """
                <?xml version="1.0" encoding="UTF-8"?>
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"
                          xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                  <sheets><sheet name="Datos" sheetId="1" r:id="rId1"/></sheets>
                </workbook>
                """);

            Add("xl/_rels/workbook.xml.rels", """
                <?xml version="1.0" encoding="UTF-8"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
                  <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings" Target="sharedStrings.xml"/>
                  <Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
                </Relationships>
                """);

            Add("xl/styles.xml", """
                <?xml version="1.0" encoding="UTF-8"?>
                <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
                  <cellXfs count="2"><xf numFmtId="0"/><xf numFmtId="14" applyNumberFormat="1"/></cellXfs>
                </styleSheet>
                """);

            var strings =
                string.Concat(
                    (sharedStrings ?? []).Select(x => $"<si><t>{System.Security.SecurityElement.Escape(x)}</t></si>"));

            Add("xl/sharedStrings.xml", $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <sst xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">{strings}</sst>
                """);

            Add("xl/worksheets/sheet1.xml", $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
                  <sheetData>{sheetXml}</sheetData>
                </worksheet>
                """);
        }

        return stream.ToArray();
    }
}
