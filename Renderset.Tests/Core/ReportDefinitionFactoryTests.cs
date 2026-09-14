using FluentAssertions;
using Renderset.Core.Definitions;
using Renderset.Core.Reports;
using Renderset.Core.Reports.Inference;

namespace Renderset.Tests.Core;

public sealed class ReportDefinitionFactoryTests
{
    [Fact]
    public void Create_ShouldBuildGeneralObjectAndTableSections()
    {
        var schema = new ReportDataSchema
        {
            Fields =
            [
                new ReportDataField
                {
                    Name = "observations",
                    Path = "observations",
                    Type = ReportDataType.String
                },
                new ReportDataField
                {
                    Name = "customer",
                    Path = "customer",
                    Type = ReportDataType.Object,
                    Children =
                    [
                        new ReportDataField
                        {
                            Name = "customerName",
                            Path = "customer.customerName",
                            Type = ReportDataType.String
                        }
                    ]
                },
                new ReportDataField
                {
                    Name = "lines",
                    Path = "lines",
                    Type = ReportDataType.Array,
                    Children =
                    [
                        new ReportDataField
                        {
                            Name = "reference",
                            Path = "lines.reference",
                            Type = ReportDataType.String
                        },
                        new ReportDataField
                        {
                            Name = "quantity",
                            Path = "lines.quantity",
                            Type = ReportDataType.Number
                        }
                    ]
                }
            ]
        };

        var result = ReportDefinitionFactory.Create(
            "invoice",
            "Invoice",
            schema);

        result.Header!.Title.Should().Be("Invoice");
        result.Footer.Should().NotBeNull();
        result.Sections.Select(x => x.Id)
            .Should().ContainInOrder("general", "customer", "lines");

        var customer = result.Sections.Single(x => x.Id == "customer");
        customer.Fields.Single().Label.Should().Be("Customer Name");

        var lines = result.Sections.Single(x => x.Id == "lines");
        lines.Table.Should().NotBeNull();
        lines.Table!.Columns.Single(x => x.Id == "lines.quantity").Type
            .Should().Be(ReportFieldType.Number);
        lines.Table.Columns.Single(x => x.Id == "lines.reference").DataPath
            .Should().Be("reference");
    }
}
