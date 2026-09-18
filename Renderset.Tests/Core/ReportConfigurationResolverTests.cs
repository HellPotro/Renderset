using FluentAssertions;
using Renderset.Core.Configurations;
using Renderset.Core.Definitions;
using Renderset.Core.Localization;
using Renderset.Core.Services;

namespace Renderset.Tests.Core;

public sealed class ReportConfigurationResolverTests
{
    private readonly ReportConfigurationResolver _sut = new();

    [Fact]
    public void Resolve_ShouldUseDefinitionDefaultsWithoutOverrides()
    {
        var definition = CreateDefinition();
        var configuration = CreateConfiguration();

        var result = _sut.Resolve(definition, configuration);

        result.Header!.Title.Should().Be("Delivery Note");
        result.Sections.Should().ContainSingle();
        result.Sections[0].Visible.Should().BeTrue();
        result.Sections[0].Fields[0].Label.Should().Be("Number");
    }

    [Fact]
    public void Resolve_ShouldApplySectionAndFieldOverrides()
    {
        var definition = CreateDefinition();
        var configuration = CreateConfiguration();
        configuration.Sections.Add(new ReportSectionConfiguration
        {
            SectionId = "general",
            Order = 99,
            Visible = false,
            Fields =
            [
                new ReportFieldConfiguration
                {
                    FieldId = "number",
                    LabelKey = "Albarán",
                    Visible = false
                }
            ]
        });

        var result = _sut.Resolve(definition, configuration);
        var section = result.GetSection("general")!;

        section.Visible.Should().BeFalse();
        section.Order.Should().Be(99);
        section.GetField("number")!.Label.Should().Be("Albarán");
        section.GetField("number")!.Visible.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldRejectConfigurationFromAnotherReport()
    {
        var definition = CreateDefinition();
        var configuration = CreateConfiguration();
        configuration.ReportId = "another-report";

        var action = () => _sut.Resolve(definition, configuration);

        action.Should().Throw<InvalidOperationException>();
    }


    [Fact]
    public void Resolve_ShouldResolveHeaderColumnsAndKeepLegacyLinesSeparate()
    {
        var definition = CreateDefinition();
        var configuration = CreateConfiguration();
        configuration.Header = new ReportHeaderConfiguration
        {
            Lines =
            [
                new ReportHeaderLineConfiguration
                {
                    Id = "legacy",
                    TextKey = "header.line.legacy",
                    Order = 10
                }
            ],
            Columns =
            [
                new ReportHeaderColumnConfiguration
                {
                    Id = "left",
                    Width = 55,
                    Lines =
                    [
                        new ReportHeaderLineConfiguration
                        {
                            Id = "company",
                            TextKey = "header.line.company",
                            Order = 10
                        }
                    ]
                }
            ]
        };

        var catalog = new ReportTextCatalog(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["header.line.legacy"] = "Legacy",
                ["header.line.company"] = "ORAN"
            });

        var result = _sut.Resolve(
            definition,
            configuration,
            catalog: catalog);

        result.Header!.Lines.Should().ContainSingle();
        result.Header.Columns.Should().ContainSingle();
        result.Header.Columns[0].Width.Should().Be(55);
        result.Header.Columns[0].Lines.Should().ContainSingle();
        result.Header.Columns[0].Lines[0].Text.Should().Be("ORAN");
    }

    private static ReportDefinition CreateDefinition() =>
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

    private static ReportConfiguration CreateConfiguration() =>
        new()
        {
            Id = "default",
            ReportId = "delivery-note",
            Name = "Default"
        };
}
