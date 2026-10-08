using System.Globalization;
using FluentAssertions;
using Renderset.Core.Configurations;
using Renderset.Core.Definitions;
using Renderset.Core.Rendering;
using Renderset.Core.Services;

namespace Renderset.Tests.Core;

public sealed class ReportValueFormatterTests
{
    private static readonly CultureInfo Spanish = new("es-ES");

    [Fact]
    public void Text_ShouldPrintNumbersWithoutGroupingOrDecimals()
    {
        ReportValueFormatter.Format(3116m, ReportFieldType.Text, Spanish).Should().Be("3116");
        ReportValueFormatter.Format(62.5m, ReportFieldType.Text, Spanish).Should().Be("62.5");
    }

    [Fact]
    public void Number_ShouldKeepTheCultureFormat()
    {
        ReportValueFormatter.Format(1480m, ReportFieldType.Number, Spanish).Should().Be("1.480,00");
    }

    [Fact]
    public void Integer_ShouldDropDecimals()
    {
        ReportValueFormatter.Format(16m, ReportFieldType.Integer, Spanish).Should().Be("16");
    }

    [Fact]
    public void NumericTypes_ShouldAcceptNumbersSentAsText()
    {
        ReportValueFormatter.Format("29.12", ReportFieldType.Number, Spanish).Should().Be("29,12");
    }
}


public sealed class ReportTableTotalsTests
{
    private static readonly object?[] Values = [16m, 5m, null, "", 2m];

    [Theory]
    [InlineData(ReportColumnTotal.Sum, 23)]
    [InlineData(ReportColumnTotal.Count, 3)]
    [InlineData(ReportColumnTotal.Min, 2)]
    [InlineData(ReportColumnTotal.Max, 16)]
    public void Compute_ShouldIgnoreEmptyValues(
        ReportColumnTotal total,
        int expected)
    {
        ReportTableTotals.Compute(Values, total).Should().Be(expected);
    }

    [Fact]
    public void Count_ShouldCountTextValuesToo()
    {
        ReportTableTotals.Compute(["PAL1", "PAL2", null], ReportColumnTotal.Count)
            .Should().Be(2);
    }

    [Fact]
    public void None_ShouldNotCompute()
    {
        ReportTableTotals.Compute(Values, ReportColumnTotal.None).Should().BeNull();
    }
}


public sealed class TypeOverrideResolverTests
{
    [Fact]
    public void Resolve_ShouldUseTheConfiguredFieldType()
    {
        var configuration = ReportSectionGridTests.Configuration();

        configuration.Sections.Add(new ReportSectionConfiguration
        {
            SectionId = "general",
            Fields =
            [
                new ReportFieldConfiguration
                {
                    FieldId = "number",
                    Type = ReportFieldType.Text
                }
            ]
        });

        new ReportConfigurationResolver()
            .Resolve(ReportSectionGridTests.Definition(), configuration)
            .GetSection("general")!
            .GetField("number")!
            .Type
            .Should()
            .Be(ReportFieldType.Text);
    }
}
