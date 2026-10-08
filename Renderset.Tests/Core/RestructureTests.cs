using System.Text.Json;
using FluentAssertions;
using Renderset.Core.DataSources;
using Renderset.Core.Definitions;
using Renderset.Core.Mapping;
using Renderset.Core.Reports;
using Renderset.Core.Reports.Inference;

namespace Renderset.Tests.Core;

/// <summary>
/// Reorganizar un report ya creado: aplanar sus datos de ejemplo con el
/// mapping, mover un campo de nivel y rehacer la definición sin perder lo
/// que no se ha movido.
/// </summary>
public sealed class RestructureTests
{
    private static DataMappingField Field(string name, string column, ReportDataType type = ReportDataType.String) =>
        new() { Name = name, SourceColumn = column, Type = type };

    private static DataMapping Mapping(bool referenciaInLines = false)
    {
        var root = new DataMappingNode
        {
            KeyColumns = ["Salida"],
            Fields = [Field("salida", "Salida", ReportDataType.Number), Field("cliente", "Cliente")]
        };

        if (!referenciaInLines)
            root.Fields.Add(Field("referencia", "Referencia"));

        var lines = new DataMappingNode
        {
            Name = "lineas",
            Kind = DataMappingNodeKind.Collection,
            Fields = [Field("articulo", "Articulo"), Field("cantidad", "Cantidad", ReportDataType.Number)]
        };

        if (referenciaInLines)
            lines.Fields.Add(Field("referencia", "Referencia"));

        var jaulas = new DataMappingNode
        {
            Name = "jaulas",
            Kind = DataMappingNodeKind.Collection,
            KeyColumns = ["Jaula"],
            Fields = [Field("jaula", "Jaula")],
            Children = [lines]
        };

        root.Children.Add(jaulas);

        return new DataMapping { Id = "m", Name = "m", Root = root };
    }

    private static readonly JsonElement Sample =
        JsonDocument.Parse(
            """
            {
              "salida": 3129,
              "cliente": "002297",
              "referencia": "240926",
              "jaulas": [
                { "jaula": "PAL1", "lineas": [ { "articulo": "A1", "cantidad": 16 }, { "articulo": "A2", "cantidad": 4 } ] },
                { "jaula": "PAL2", "lineas": [ { "articulo": "A3", "cantidad": 29 } ] },
                { "jaula": "PAL3", "lineas": [] }
              ]
            }
            """).RootElement.Clone();

    [Fact]
    public async Task Flatten_ShouldGiveBackRowsThatRebuildTheSameDocument()
    {
        var rows = DataMappingFlattener.Flatten(Mapping(), Sample);

        // Una fila por línea y una más por la jaula sin líneas (LEFT JOIN).
        rows.Rows.Should().HaveCount(4);
        rows.Columns.Select(x => x.Name).Should().Contain(new[] { "Salida", "Cliente", "Referencia", "Jaula", "Articulo", "Cantidad" });

        var rebuilt = (await TabularDocuments.BuildAsync(rows, Mapping())).Single();

        rebuilt.GetProperty("referencia").GetString().Should().Be("240926");
        rebuilt.GetProperty("jaulas").GetArrayLength().Should().Be(3);
        rebuilt.GetProperty("jaulas")[0].GetProperty("lineas").GetArrayLength().Should().Be(2);
        rebuilt.GetProperty("jaulas")[0].GetProperty("lineas")[1].GetProperty("cantidad").GetDecimal().Should().Be(4);
    }

    [Fact]
    public async Task Flatten_ShouldLetAFieldMoveToAnotherLevel()
    {
        var rows = DataMappingFlattener.Flatten(Mapping(), Sample);

        var moved = (await TabularDocuments.BuildAsync(rows, Mapping(referenciaInLines: true))).Single();

        moved.TryGetProperty("referencia", out _).Should().BeFalse();
        moved.GetProperty("jaulas")[0].GetProperty("lineas")[0].GetProperty("referencia").GetString().Should().Be("240926");
    }

    [Fact]
    public void Merge_ShouldKeepWhatDidNotMoveAndPlaceTheMovedField()
    {
        var before =
            ReportDefinitionFactory.Create("r", "R", DataMappingSchemaFactory.Create(Mapping()));

        // Lo que el usuario había tocado en la definición.
        var general = before.Sections.Single(x => x.Id == "general");
        before.Sections[before.Sections.IndexOf(general)] = new ReportSectionDefinition
        {
            Id = general.Id,
            Name = "Cabecera de la salida",
            Order = general.Order,
            Fields = general.Fields
        };

        var after =
            ReportDefinitionMerger.Merge(
                before,
                ReportDefinitionFactory.Create("r", "R", DataMappingSchemaFactory.Create(Mapping(referenciaInLines: true))));

        after.Version.Should().Be(before.Version + 1);

        var newGeneral = after.Sections.Single(x => x.Id == "general");
        newGeneral.Name.Should().Be("Cabecera de la salida");
        newGeneral.Fields.Select(x => x.Id).Should().Equal("salida", "cliente");

        var lines =
            after.Sections.Single(x => x.Id == "jaulas")
                .Sections.Single(x => x.Id == "jaulas.lineas")
                .Table!;

        lines.Columns.Select(x => x.Id).Should().Contain("jaulas.lineas.referencia");

        // Lo nuevo va detrás de lo que ya había.
        lines.Columns.Single(x => x.Id == "jaulas.lineas.referencia").Order
            .Should().BeGreaterThan(lines.Columns.Where(x => x.Id != "jaulas.lineas.referencia").Max(x => x.Order));
    }
}
