using FluentAssertions;
using Renderset.Core.DataSources;

namespace Renderset.Tests.Core;

public sealed class DelimitedTextParserTests
{
    [Fact]
    public void Parse_ShouldJoinRowsBrokenByALineBreakInsideACell()
    {
        // Copiado de SSMS: Direccion lleva un salto de línea y la fila llega
        // en dos líneas, sin comillas.
        const string text =
            "Salida\tDireccion\tCpostal\tJaula\n" +
            "3129\tPol. Ind. La Red Sur\n" +
            "C/ La Red, Uno nº 43\t41500\tPAL234923\n" +
            "3129\tPol. Ind. La Red Sur\n" +
            "C/ La Red, Uno nº 43\t41500\tPAL234924\n";

        var payload = DelimitedTextParser.Parse(text);

        payload.Rows.Should().HaveCount(2);
        payload.Rows.Should().OnlyContain(x => x.Count == 4);

        payload.Rows[0][0].Should().Be(3129m);
        payload.Rows[0][1].Should().Be("Pol. Ind. La Red Sur\nC/ La Red, Uno nº 43");
        payload.Rows[1][3].Should().Be("PAL234924");
    }

    [Fact]
    public void Parse_ShouldRenameRepeatedHeaders()
    {
        const string text =
            "Direccion\tPais\tDireccion\tPais\tDireccion\n" +
            "Pol. Ind.\tSpain\tAvda. Parayas 0\tSpain\tOtra\n";

        DelimitedTextParser.Parse(text)
            .Columns
            .Select(x => x.Name)
            .Should()
            .Equal("Direccion", "Pais", "Direccion_2", "Pais_2", "Direccion_3");
    }

    [Fact]
    public void Parse_ShouldKeepQuotedLineBreaksAndSeparators()
    {
        const string text =
            "Nombre;Direccion\n" +
            "\"Geimex, S.A.\";\"Pol. Ind.; nave 3\nSevilla\"\n";

        var payload = DelimitedTextParser.Parse(text);

        payload.Rows.Should().ContainSingle();
        payload.Rows[0][1].Should().Be("Pol. Ind.; nave 3\nSevilla");
    }

    [Fact]
    public void Parse_ShouldTreatAQuoteInsideAValueAsText()
    {
        const string text =
            "Articulo\tDescripcion\n" +
            "A1\tTubo 3\" acero\n" +
            "A2\tTubo 4\" acero\n";

        var payload = DelimitedTextParser.Parse(text);

        payload.Rows.Should().HaveCount(2);
        payload.Rows[1][1].Should().Be("Tubo 4\" acero");
    }
}
