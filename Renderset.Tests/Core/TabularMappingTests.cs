using System.Text.Json;
using FluentAssertions;
using Renderset.Core.DataSources;
using Renderset.Core.Mapping;
using Renderset.Core.Reports;

namespace Renderset.Tests.Core;

/// <summary>
/// El packing list de una salida: documento → albaranes → jaulas → artículos,
/// a partir de las filas planas del SELECT.
/// </summary>
public sealed class TabularMappingTests
{
    private static readonly string[] Columns =
        ["SalidaId", "Nombre", "Idioma", "AlbaranEjercicio", "Referencia", "Jaula", "Articulo", "Descripcion_esp", "Cantidad", "Peso"];

    private static TabularPayload Rows(params object?[][] rows) =>
        new()
        {
            Columns = Columns
                .Select(name => new TabularPayloadColumn
                {
                    Name = name,
                    Type = name is "SalidaId" or "Cantidad" or "Peso"
                        ? ReportDataType.Number
                        : ReportDataType.String
                })
                .ToList(),
            Rows = rows.Select(row => row.ToList()).ToList()
        };

    private static TabularPayload SalidaRows() =>
        Rows(
            [3116, "Inter Cars, S.A.", "Ingles    ", "001963/2026", "7th", "PAL235071 ", "00119012.A2         ", "Aleta Del. D. Seat-Ibiza 2008   ", 16, 29.12m],
            [3116, "Inter Cars, S.A.", "Ingles    ", "001963/2026", "7th", "PAL235071 ", "00465030.C2         ", "Capot Del. Ford-Fiesta 2008", 5, 54.10m],
            [3116, "Inter Cars, S.A.", "Ingles    ", "001963/2026", "7th", "PAL235072 ", "00158011.A2         ", "Aleta Del. I. Seat-Leon 2012", 8, 24m],
            [3116, "Inter Cars, S.A.", "Ingles    ", "001963/2026", "7th", "PAL235073 ", "00690011.A2         ", "Aleta Del. I. Citroen-Berlingo 2018", 4, 8.24m]);

    private static DataMappingField Field(string name, string column, ReportDataType type = ReportDataType.String) =>
        new() { Name = name, SourceColumn = column, Type = type };

    private static DataMapping PackingList() =>
        new()
        {
            Id = "packing-list-2026-rows",
            Name = "Packing list (filas)",
            ReportId = "packing-list-2026",
            Root = new DataMappingNode
            {
                KeyColumns = ["SalidaId"],
                Fields =
                [
                    Field("salidaid", "SalidaId", ReportDataType.Number),
                    Field("nombre", "Nombre"),
                    Field("idioma", "Idioma")
                ],
                Children =
                [
                    new DataMappingNode
                    {
                        Name = "albaranes",
                        Kind = DataMappingNodeKind.Collection,
                        KeyColumns = ["AlbaranEjercicio"],
                        Fields =
                        [
                            Field("albaranejercicio", "AlbaranEjercicio"),
                            Field("referencia", "Referencia")
                        ],
                        Children =
                        [
                            new DataMappingNode
                            {
                                Name = "jaulas",
                                Kind = DataMappingNodeKind.Collection,
                                KeyColumns = ["Jaula"],
                                Fields = [Field("jaula", "Jaula")],
                                Children =
                                [
                                    new DataMappingNode
                                    {
                                        Name = "articulos",
                                        Kind = DataMappingNodeKind.Collection,
                                        Fields =
                                        [
                                            Field("articulo", "Articulo"),
                                            Field("descripcionEsp", "Descripcion_esp"),
                                            Field("cantidad", "Cantidad", ReportDataType.Number),
                                            Field("peso", "Peso", ReportDataType.Number)
                                        ]
                                    }
                                ]
                            }
                        ]
                    }
                ]
            }
        };

    [Fact]
    public async Task Build_ShouldNestAlbaranesJaulasAndArticulos()
    {
        var documents =
            await TabularDocuments.BuildAsync(SalidaRows(), PackingList());

        documents.Should().ContainSingle();

        var document = documents[0];
        var albaranes = document.GetProperty("albaranes");

        albaranes.GetArrayLength().Should().Be(1);

        var jaulas = albaranes[0].GetProperty("jaulas");

        jaulas.GetArrayLength().Should().Be(3);
        jaulas[0].GetProperty("articulos").GetArrayLength().Should().Be(2);
        jaulas[2].GetProperty("articulos")[0].GetProperty("cantidad").GetDecimal().Should().Be(4);
    }

    [Fact]
    public async Task Build_ShouldTrimPaddedCharColumns()
    {
        var document =
            (await TabularDocuments.BuildAsync(SalidaRows(), PackingList()))[0];

        document.GetProperty("idioma").GetString().Should().Be("Ingles");

        document.GetProperty("albaranes")[0]
            .GetProperty("jaulas")[0]
            .GetProperty("jaula").GetString()
            .Should().Be("PAL235071");
    }

    [Fact]
    public async Task Build_ShouldReadNumericTextInvariantly()
    {
        // Un número que llega como texto ("29.12") no puede leerse con la
        // cultura del servidor: en es-ES saldría 2912.
        var previous = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("es-ES");

        try
        {
            var rows =
                Rows([3116, "X", "Ingles", "1/2026", "r", "J1", "A1", "D", "16", "29.12"]);

            var peso =
                (await TabularDocuments.BuildAsync(rows, PackingList()))[0]
                    .GetProperty("albaranes")[0]
                    .GetProperty("jaulas")[0]
                    .GetProperty("articulos")[0]
                    .GetProperty("peso")
                    .GetDecimal();

            peso.Should().Be(29.12m);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }

    [Fact]
    public async Task Build_ShouldKeepAValueThatDoesNotFitAsText()
    {
        // "prioridades julio" en una columna que el mapping cree numérica:
        // el documento tiene que salir igual.
        var rows =
            Rows([3116, "X", "Ingles", "1/2026", "r", "J1", "A1", "D", "dieciséis", 1]);

        var cantidad =
            (await TabularDocuments.BuildAsync(rows, PackingList()))[0]
                .GetProperty("albaranes")[0]
                .GetProperty("jaulas")[0]
                .GetProperty("articulos")[0]
                .GetProperty("cantidad");

        cantidad.GetString().Should().Be("dieciséis");
    }

    [Fact]
    public async Task Build_ShouldReportTheColumnOfAnInvalidValueWhenStrict()
    {
        var rows =
            Rows([3116, "X", "Ingles", "1/2026", "r", "J1", "A1", "D", "dieciséis", 1]);

        var action = async () =>
        {
            await using var reader = new TabularPayloadDataSet(rows);

            await foreach (var _ in new HierarchyBuilder().BuildAsync(
                               reader,
                               PackingList(),
                               new HierarchyBuilderOptions { StrictTypes = true }))
            {
            }
        };

        (await action.Should().ThrowAsync<DataMappingValueException>())
            .Which.SourceColumn.Should().Be("Cantidad");
    }

    [Fact]
    public void Validate_ShouldListMissingColumns()
    {
        var rows = SalidaRows();
        rows.Columns.RemoveAt(rows.Columns.FindIndex(x => x.Name == "Peso"));

        foreach (var row in rows.Rows)
            row.RemoveAt(row.Count - 1);

        TabularDocuments.Validate(rows, PackingList())
            .Should()
            .ContainSingle()
            .Which.Should().Contain("Peso");
    }

    [Fact]
    public void Validate_ShouldRejectRowsWithTheWrongWidth()
    {
        var rows = SalidaRows();
        rows.Rows[1].RemoveAt(0);

        TabularDocuments.Validate(rows, PackingList())
            .Should()
            .ContainSingle()
            .Which.Should().Contain("fila 1");
    }

    [Fact]
    public void Rules_ShouldRejectDuplicatedFieldNames()
    {
        var mapping = PackingList();
        mapping.Root.Fields.Add(Field("Nombre", "Idioma"));

        DataMappingRules.Validate(mapping)
            .Should()
            .ContainSingle()
            .Which.Should().Contain("Nombre");
    }

    [Fact]
    public void CompareWithReport_ShouldFlagWhatTheReportUsesAndTheMappingDoesNotProduce()
    {
        var reportSchema =
            new ReportDataSchema
            {
                Fields =
                [
                    new ReportDataField { Name = "salidaid", Path = "salidaid", Type = ReportDataType.Number },
                    new ReportDataField { Name = "matricula", Path = "matricula", Type = ReportDataType.String },
                    new ReportDataField
                    {
                        Name = "albaranes",
                        Path = "albaranes",
                        Type = ReportDataType.Array,
                        Children =
                        [
                            new ReportDataField { Name = "referencia", Path = "albaranes.referencia", Type = ReportDataType.String }
                        ]
                    }
                ]
            };

        var issues =
            DataMappingRules.CompareWithReport(PackingList(), reportSchema);

        issues.Where(x => x.IsWarning)
            .Select(x => x.Path)
            .Should()
            .BeEquivalentTo(new[] { "matricula" });

        issues.Should().Contain(x =>
            x.Kind == DataMappingCompatibilityKind.NotInReport &&
            x.Path == "albaranes.jaulas.articulos.peso");
    }

    [Fact]
    public void Mapping_ShouldRoundTripThroughJsonWithStringEnums()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };

        var json = JsonSerializer.Serialize(PackingList().Root, options);
        var back = JsonSerializer.Deserialize<DataMappingNode>(json, options)!;

        json.Should().Contain("\"Collection\"");
        back.Children[0].Children[0].Children[0].Name.Should().Be("articulos");
    }
}
