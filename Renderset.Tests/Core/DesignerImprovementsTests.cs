using FluentAssertions;
using Renderset.Core.Configurations;
using Renderset.Core.Definitions;
using Renderset.Core.Services;
using Renderset.Core.Themes;

namespace Renderset.Tests.Core;

public sealed class ReportSectionGridTests
{
    [Theory]
    [InlineData(ReportSectionLayout.Columns2, null, 2)]
    [InlineData(ReportSectionLayout.Columns3, null, 3)]
    [InlineData(ReportSectionLayout.Columns4, 9, 4)]
    [InlineData(ReportSectionLayout.Grid, 6, 6)]
    [InlineData(ReportSectionLayout.Grid, 1, 1)]
    [InlineData(ReportSectionLayout.Grid, 40, 12)]
    [InlineData(ReportSectionLayout.Grid, null, 2)]
    public void Normalize_ShouldTurnLegacyAndNewLayoutsIntoAGrid(
        ReportSectionLayout layout,
        int? columns,
        int expected)
    {
        var grid = ReportSectionGrid.Normalize(layout, columns);

        grid.Layout.Should().Be(ReportSectionLayout.Grid);
        grid.Columns.Should().Be(expected);
    }

    [Fact]
    public void Normalize_ShouldKeepListWithoutStoredColumns()
    {
        var grid = ReportSectionGrid.Normalize(null, 5);

        grid.IsList.Should().BeTrue();
        grid.StoredColumns.Should().BeNull();
    }

    [Fact]
    public void Resolve_ShouldReadLegacyColumnsLayoutAsGrid()
    {
        var configuration = Configuration();
        configuration.Sections.Add(new ReportSectionConfiguration
        {
            SectionId = "general",
            Layout = ReportSectionLayout.Columns3
        });

        var section =
            new ReportConfigurationResolver()
                .Resolve(Definition(), configuration)
                .GetSection("general")!;

        section.Layout.Should().Be(ReportSectionLayout.Grid);
        section.Columns.Should().Be(3);
    }

    [Fact]
    public void Resolve_ShouldSupportSixColumns()
    {
        var configuration = Configuration();
        configuration.Sections.Add(new ReportSectionConfiguration
        {
            SectionId = "general",
            Layout = ReportSectionLayout.Grid,
            Columns = 6
        });

        var section =
            new ReportConfigurationResolver()
                .Resolve(Definition(), configuration)
                .GetSection("general")!;

        section.Columns.Should().Be(6);
    }

    [Fact]
    public void Resolve_ShouldHideTitleWithoutLosingItsText()
    {
        var configuration = Configuration();
        configuration.Header = new ReportHeaderConfiguration
        {
            ShowTitle = false
        };

        var header =
            new ReportConfigurationResolver()
                .Resolve(Definition(), configuration)
                .Header!;

        header.ShowTitle.Should().BeFalse();
        header.ShowSubtitle.Should().BeTrue();
        header.Title.Should().Be("Delivery Note");
    }

    internal static ReportDefinition Definition() =>
        new()
        {
            Id = "delivery-note",
            Name = "Delivery Note",
            Header = new ReportHeaderDefinition
            {
                Title = "Delivery Note"
            },
            Sections =
            [
                new ReportSectionDefinition
                {
                    Id = "general",
                    Name = "General",
                    Order = 10,
                    Fields =
                    [
                        new ReportFieldDefinition
                        {
                            Id = "number",
                            Label = "Number",
                            DataPath = "number",
                            Order = 10
                        }
                    ]
                }
            ]
        };

    internal static ReportConfiguration Configuration() =>
        new()
        {
            Id = "default",
            ReportId = "delivery-note",
            Name = "Default"
        };
}


public sealed class ReportThemeRulesTests
{
    [Fact]
    public void Validate_ShouldAcceptTheDefaultTheme()
    {
        ReportThemeRules.Validate(new ReportTheme())
            .Should()
            .BeEmpty();
    }

    [Theory]
    [InlineData("red")]
    [InlineData("#12345")]
    [InlineData("#000;background:url(x)")]
    public void Validate_ShouldRejectAnythingButHexColors(
        string color)
    {
        var theme = new ReportTheme { PrimaryColor = color };

        ReportThemeRules.Validate(theme)
            .Should()
            .ContainSingle();
    }

    [Fact]
    public void Validate_ShouldRejectFontFamiliesThatEscapeTheDeclaration()
    {
        var theme = new ReportTheme { FontFamily = "Arial; color: red" };

        ReportThemeRules.Validate(theme)
            .Should()
            .ContainSingle();
    }

    [Fact]
    public void Validate_ShouldAcceptEveryOfferedFont()
    {
        foreach (var (_, stack) in ReportThemeFonts.All)
        {
            ReportThemeRules.IsFontFamily(stack)
                .Should()
                .BeTrue(stack);
        }
    }

    [Theory]
    [InlineData("ORAN Compacto", "oran-compacto")]
    [InlineData("  Cálido · Serif  ", "calido-serif")]
    [InlineData("!!!", "tema")]
    public void SlugFrom_ShouldBuildAValidId(
        string name,
        string expected)
    {
        ReportThemeRules.SlugFrom(name)
            .Should()
            .Be(expected);
    }

    [Fact]
    public void Css_ShouldOnlyEmitOptionalVariablesWhenSet()
    {
        var css = ReportThemeCss.Variables(new ReportTheme());

        css.Should().Contain("--report-primary:#00285A;");
        css.Should().NotContain("--report-title");
        css.Should().NotContain("--report-table-stripe");

        css = ReportThemeCss.Variables(
            new ReportTheme { TableStripeColor = "#F4F7F9" });

        css.Should().Contain("--report-table-stripe:#F4F7F9;");
    }
}
