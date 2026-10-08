using FluentAssertions;
using Renderset.Core.Definitions;
using Renderset.Core.Localization;

namespace Renderset.Tests.Core;

public sealed class ReportResourceSeederTests
{
    [Fact]
    public void Build_ShouldSeedNestedSectionsFieldsAndColumns()
    {
        var definition =
            new ReportDefinition
            {
                Id = "packing-list-por-jaula",
                Name = "Packing list por jaula",
                Sections =
                [
                    new ReportSectionDefinition
                    {
                        Id = "jaulas",
                        Name = "Jaulas",
                        DataPath = "jaulas",
                        Fields =
                        [
                            new ReportFieldDefinition { Id = "jaulas.jaula", Label = "Jaula", DataPath = "jaula" }
                        ],
                        Sections =
                        [
                            new ReportSectionDefinition
                            {
                                Id = "jaulas.lineas",
                                Name = "Lineas",
                                Table = new ReportTableDefinition
                                {
                                    Id = "jaulas.lineas.table",
                                    Name = "Lineas",
                                    DataPath = "lineas",
                                    Columns =
                                    [
                                        new ReportColumnDefinition { Id = "articulo", Label = "Articulo", DataPath = "articulo" },
                                        new ReportColumnDefinition { Id = "sinonimo", Label = "Sinonimo", DataPath = "sinonimo" }
                                    ]
                                }
                            }
                        ]
                    }
                ]
            };

        var keys =
            ReportResourceSeeder
                .Build(definition, "es-ES")
                .Select(x => x.Key)
                .ToList();

        keys.Should().Contain(
        [
            ReportTextKeys.Section("jaulas.lineas"),
            ReportTextKeys.Column("jaulas.lineas.table", "articulo"),
            ReportTextKeys.Column("jaulas.lineas.table", "sinonimo")
        ]);
    }
}
