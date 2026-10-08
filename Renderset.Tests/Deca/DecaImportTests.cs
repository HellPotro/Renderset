using FluentAssertions;
using Renderset.Core.DataSources;
using Renderset.Deca;
using Renderset.Deca.Import;
using Renderset.Deca.Validation;

namespace Renderset.Tests.Deca;

public sealed class DecaImportTests
{
    private static readonly TimeZoneInfo Zone = new DecaOptions().Zone;

    private static readonly DateOnly Tomorrow =
        DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));

    private static DecaImportResult ReadCsv(
        string csv) =>
        DecaImport.Read(
            TabularFile.Read(
                System.Text.Encoding.UTF8.GetBytes(csv),
                "deca.csv",
                new TabularFileOptions { InferTypes = false }).Payload,
            decimalPoint: false);

    [Fact]
    public void Template_ShouldReadBackAsTwoValidDecas()
    {
        var result = ReadCsv(DecaImport.Template(Tomorrow));

        result.Errors.Should().BeEmpty();
        result.IgnoredColumns.Should().BeEmpty();
        result.Items.Should().HaveCount(2);

        var first = result.Items[0];

        first.Key.Should().Be("1");
        first.Rows.Should().Equal(2, 3);
        first.Data.Shipments.Should().HaveCount(2);
        first.Data.TransportDate.Should().Be(Tomorrow);
        first.Data.StartTime.Should().Be(new TimeOnly(7, 30));
        first.Data.Shipments[1].AlternativeQuantity.Should().Be(22);
        first.Data.Shipments[1].AlternativeUnit.Should().Be("jaulas");

        foreach (var item in result.Items)
        {
            item.Problems.Should().BeEmpty();

            DecaValidator.Validate(
                    DecaNormalizer.Normalize(item.Data),
                    DateTimeOffset.UtcNow,
                    Zone)
                .Errors.Should().BeEmpty(item.Key);
        }
    }

    [Fact]
    public void Read_ShouldGroupRowsThatShareTheDocumentWithoutAGroupColumn()
    {
        var csv =
            "cargador;nif cargador;domicilio cargador;transportista;nif transportista;fecha;matricula;origen;destino;mercancia;peso\n" +
            "Talleres;B12345674;Calle 1;Trans;A87654323;09/10/2026;1234BCD;A;B;Piezas;12.500\n" +
            "Talleres;b-12345674;Calle 1;Trans;A87654323;09/10/2026;1234 BCD;A;C;Piezas;1.234,5\n" +
            "Talleres;B12345674;Calle 1;Trans;A87654323;10/10/2026;1234BCD;A;D;Piezas;12.5\n";

        var result = ReadCsv(csv);

        result.Errors.Should().BeEmpty();
        result.Items.Should().HaveCount(2);

        // 09/10/2026 es el 9 de octubre, no el 10 de septiembre.
        result.Items[0].Data.TransportDate.Should().Be(new DateOnly(2026, 10, 9));
        result.Items[0].Data.Shipments.Select(x => x.WeightKg).Should().Equal(12500m, 1234.5m);

        result.Items[1].Data.Shipments.Single().WeightKg.Should().Be(12.5m);
    }

    [Fact]
    public void Read_ShouldWarnWhenARowOfTheSameDecaDisagrees()
    {
        var csv =
            "DeCA;Cargador razón social;Cargador NIF;Cargador domicilio;Transportista razón social;Transportista NIF;Fecha transporte;Matrícula tractora;Origen;Destino;Mercancía;Peso kg\n" +
            "7;Talleres;B12345674;Calle 1;Trans;A87654323;09/10/2026;1234BCD;A;B;Piezas;100\n" +
            "7;Talleres;B12345674;Calle 1;Otra;A87654323;09/10/2026;1234BCD;A;C;Piezas;200\n";

        var item = ReadCsv(csv).Items.Single();

        item.Data.Carrier.Name.Should().Be("Trans");
        item.Data.Shipments.Should().HaveCount(2);
        item.Problems.Should().ContainSingle(x => x.Contains("Transportista razón social") && x.Contains("Fila 3"));
    }

    [Fact]
    public void Read_ShouldReportMissingColumnsAndUnreadableValues()
    {
        var csv =
            "Cargador razón social;Fecha transporte;Peso kg;Color\n" +
            "Talleres;mañana;mucho;rojo\n";

        var result = ReadCsv(csv);

        result.Errors.Should().Contain(x => x.Contains("Cargador NIF"));
        result.Errors.Should().Contain(x => x.Contains("Matrícula tractora"));
        result.IgnoredColumns.Should().Equal("Color");

        var problems = result.Items.Single().Problems;

        problems.Should().Contain(x => x.Contains("fecha «mañana»"));
        problems.Should().Contain(x => x.Contains("peso «mucho»"));
    }

    [Theory]
    [InlineData("12500", false, 12500)]
    [InlineData("12.500", false, 12500)]
    [InlineData("12.500,75", false, 12500.75)]
    [InlineData("12,5", false, 12.5)]
    [InlineData("12.5", false, 12.5)]
    [InlineData("12500.5", true, 12500.5)]
    [InlineData("12.500", true, 12500)]
    [InlineData("12,5", true, 12.5)]
    [InlineData("1 200 kg", false, 1200)]
    public void TryNumber_ShouldReadSpanishAndExcelNumbers(
        string value,
        bool decimalPoint,
        double expected)
    {
        DecaImport.TryNumber(value, decimalPoint, out var number).Should().BeTrue();
        number.Should().Be((decimal)expected);
    }

    [Theory]
    [InlineData("Matrícula tractora", "matriculatractora")]
    [InlineData("MATRICULA_TRACTORA", "matriculatractora")]
    [InlineData(" Peso (kg) ", "pesokg")]
    public void Normalize_ShouldIgnoreAccentsCaseAndSymbols(
        string header,
        string expected)
    {
        DecaImport.Normalize(header).Should().Be(expected);
    }

    [Fact]
    public void TabularFile_ShouldKeepTextWhenAskedNotToInferTypes()
    {
        var payload =
            TabularFile.Read(
                "Fecha;Peso\n09/10/2026;00123\n"u8.ToArray(),
                "x.csv",
                new TabularFileOptions { InferTypes = false }).Payload;

        payload.Rows[0].Should().Equal("09/10/2026", "00123");
    }
}
