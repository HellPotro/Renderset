using FluentAssertions;
using Renderset.Core.Configurations;
using Renderset.Core.Localization;
using Renderset.Core.Rendering;
using Renderset.Core.Services;
using Renderset.Deca.Issuing;

namespace Renderset.Tests.Deca;

public sealed class DecaTemplateGuardTests
{
    private static ReportConfiguration Base() =>
        DecaReportTemplate.Configuration(branding: null);

    private static IReadOnlyList<string> MissingOf(
        ReportConfiguration configuration) =>
        DecaTemplateGuard.Missing(
            new ReportConfigurationResolver().Resolve(
                DecaReportTemplate.Definition,
                configuration,
                bodyBlocks: null,
                ReportTextCatalog.Empty));

    [Fact]
    public void BaseDesign_ShouldShowEverythingRequired()
    {
        MissingOf(Base()).Should().BeEmpty();
    }

    [Fact]
    public void Missing_ShouldNameWhatADesignHides()
    {
        var configuration = Base();

        configuration.Sections.Single(x => x.SectionId == "cargador").Fields.Add(
            new ReportFieldConfiguration { FieldId = "cargador.nif", Visible = false });

        configuration.Sections.Add(
            new ReportSectionConfiguration
            {
                SectionId = "envios",
                Table = new ReportTableConfiguration
                {
                    TableId = "envios.table",
                    Columns = [new ReportColumnConfiguration { ColumnId = "envios.cantidad", Visible = false }]
                }
            });

        configuration.Header!.ShowQr = false;

        MissingOf(configuration)
            .Should().BeEquivalentTo(new[]
            {
                "NIF del cargador",
                "Peso o magnitud",
                "Código QR de descarga del PDF"
            });
    }

    [Fact]
    public void Enforce_ShouldBringBackRequiredDataAndKeepTheRest()
    {
        var configuration = Base();

        var transport = configuration.Sections.Single(x => x.SectionId == "transporte");

        transport.Fields.Add(new ReportFieldConfiguration { FieldId = "transporte.tractora", Visible = false, LabelKey = "Camión" });

        // Opcional: se puede quitar.
        transport.Fields.Add(new ReportFieldConfiguration { FieldId = "transporte.referencia", Visible = false });

        configuration.Sections.Single(x => x.SectionId == "emision").Visible = false;

        configuration.Header!.ShowQr = false;
        configuration.Header.QrContent = "https://otra-cosa";
        configuration.Header.QrSize = 56;

        var enforced = DecaTemplateGuard.Enforce(configuration);

        MissingOf(enforced).Should().BeEmpty();

        var fields = enforced.Sections.Single(x => x.SectionId == "transporte").Fields;

        fields.Single(x => x.FieldId == "transporte.tractora").Visible.Should().BeNull();
        fields.Single(x => x.FieldId == "transporte.tractora").LabelKey.Should().Be("Camión");
        fields.Single(x => x.FieldId == "transporte.referencia").Visible.Should().BeFalse();

        enforced.Header!.ShowQr.Should().BeTrue();
        enforced.Header.QrContent.Should().Be(ReportQrContent.PdfUrlTemplate);
        enforced.Header.QrSize.Should().Be(DecaTemplateGuard.MinQrSize);

        // El original no se toca.
        configuration.Header.ShowQr.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldUseTheTenantDesignAndItsVersion()
    {
        var configuration = Base();
        configuration.Header!.TitlePlacement = Renderset.Core.Definitions.ReportHeaderTitlePlacement.Above;

        var design =
            DecaTemplateGuard.Resolve(
                new DecaTemplate
                {
                    TenantId = "oranauto",
                    Version = 4,
                    Configuration = configuration,
                    Texts = new Dictionary<string, string>
                    {
                        [ReportTextKeys.Field("cargador.nif")] = "CIF"
                    }
                },
                branding: null);

        design.TemplateVersion.Should().Be(4);
        design.Rejected.Should().BeEmpty();
        design.Resolved.Header!.TitlePlacement.Should().Be(Renderset.Core.Definitions.ReportHeaderTitlePlacement.Above);
        design.Resolved.GetSection("cargador")!.GetField("cargador.nif")!.Label.Should().Be("CIF");
    }

    [Fact]
    public void Resolve_ShouldUseTheBaseWithoutTenantDesign()
    {
        var design = DecaTemplateGuard.Resolve(null, branding: null);

        design.TemplateVersion.Should().Be(DecaTemplate.BaseVersion);
        DecaTemplateGuard.Missing(design.Resolved).Should().BeEmpty();
    }

    [Fact]
    public void Resolve_ShouldTreatAResetAsTheBase()
    {
        var design =
            DecaTemplateGuard.Resolve(
                new DecaTemplate { TenantId = "oranauto", Version = 5, Configuration = null },
                branding: null);

        design.TemplateVersion.Should().Be(DecaTemplate.BaseVersion);
    }
}
