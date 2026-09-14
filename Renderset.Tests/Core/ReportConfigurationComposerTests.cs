using FluentAssertions;
using Renderset.Core.Blocks;
using Renderset.Core.Configurations;
using Renderset.Core.Services;

namespace Renderset.Tests.Core;

public sealed class ReportConfigurationComposerTests
{
    private readonly IReportConfigurationComposer _sut =
        new ReportConfigurationComposer();

    [Fact]
    public void Compose_ShouldApplyHeaderBlockDefaults()
    {
        var configuration = CreateConfiguration();
        configuration.Header = new ReportHeaderConfiguration
        {
            BlockId = "oran-header"
        };

        var block = new ReportBlock
        {
            Id = "oran-header",
            Name = "ORAN Header",
            Type = ReportBlockType.Header,
            ConfigurationJson = """
            {
              "subtitleOverride": "Talleres ORAN S.L.U.",
              "showLogo": true,
              "logoUrl": "https://example.com/logo.png"
            }
            """
        };

        var result = _sut.Compose(configuration, block);

        result.Header.Should().NotBeNull();
        result.Header!.SubtitleKey.Should().Be("Talleres ORAN S.L.U.");
        result.Header.ShowLogo.Should().BeTrue();
        result.Header.LogoUrl.Should().Be("https://example.com/logo.png");
    }

    [Fact]
    public void Compose_ShouldKeepPresetOverridesOverBlockValues()
    {
        var configuration = CreateConfiguration();
        configuration.Header = new ReportHeaderConfiguration
        {
            BlockId = "oran-header",
            SubtitleKey = "Override del preset",
            ShowLogo = false
        };

        var block = new ReportBlock
        {
            Id = "oran-header",
            Name = "ORAN Header",
            Type = ReportBlockType.Header,
            ConfigurationJson = """
            {
              "subtitleOverride": "Valor del bloque",
              "showLogo": true
            }
            """
        };

        var result = _sut.Compose(configuration, block);

        result.Header!.SubtitleKey.Should().Be("Override del preset");
        result.Header.ShowLogo.Should().BeFalse();
    }

    [Fact]
    public void Compose_ShouldNotMutateSourceConfiguration()
    {
        var configuration = CreateConfiguration();
        configuration.Header = new ReportHeaderConfiguration
        {
            BlockId = "oran-header"
        };

        var block = new ReportBlock
        {
            Id = "oran-header",
            Name = "ORAN Header",
            Type = ReportBlockType.Header,
            ConfigurationJson = """{ "subtitleOverride": "Empresa" }"""
        };

        var result = _sut.Compose(configuration, block);

        configuration.Header.SubtitleKey.Should().BeNull();
        result.Header!.SubtitleKey.Should().Be("Empresa");
    }

    [Fact]
    public void Compose_ShouldApplyFooterBlockDefaults()
    {
        var configuration = CreateConfiguration();
        configuration.Footer = new ReportFooterConfiguration
        {
            BlockId = "oran-footer"
        };

        var block = new ReportBlock
        {
            Id = "oran-footer",
            Name = "ORAN Footer",
            Type = ReportBlockType.Footer,
            ConfigurationJson = """
            {
              "textOverride": "Documento generado por ORAN",
              "showGenerationDate": true
            }
            """
        };

        var result = _sut.Compose(configuration, footerBlock: block);

        result.Footer!.TextKey.Should().Be("Documento generado por ORAN");
        result.Footer.ShowGenerationDate.Should().BeTrue();
    }

    [Fact]
    public void Compose_ShouldRejectWrongBlockType()
    {
        var configuration = CreateConfiguration();
        configuration.Header = new ReportHeaderConfiguration
        {
            BlockId = "wrong"
        };

        var block = new ReportBlock
        {
            Id = "wrong",
            Name = "Wrong",
            Type = ReportBlockType.Footer,
            ConfigurationJson = "{}"
        };

        var action = () => _sut.Compose(configuration, block);

        action.Should().Throw<InvalidOperationException>();
    }

    private static ReportConfiguration CreateConfiguration() =>
        new()
        {
            Id = "default",
            ReportId = "delivery-note",
            Name = "Default"
        };
}
