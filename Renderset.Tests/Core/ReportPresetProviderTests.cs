using FluentAssertions;
using Moq;
using Renderset.Core.Configurations;
using Renderset.Core.Persistence;
using Renderset.Core.Presets;
using Renderset.Core.Themes;

namespace Renderset.Tests.Core;

public sealed class ReportPresetProviderTests
{
    [Fact]
    public async Task GetPresetAsync_ShouldPreferContextAssignmentOverDefault()
    {
        var presets = new Mock<IReportPresetRepository>();
        var assignments = new Mock<IReportPresetAssignmentRepository>();

        assignments
            .Setup(x => x.GetAsync(
                "oranauto",
                "delivery-note",
                "customer",
                "000762",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReportPresetAssignment
            {
                ReportId = "delivery-note",
                ContextType = "customer",
                ContextKey = "000762",
                PresetId = "vanwezel"
            });

        presets
            .Setup(x => x.GetByIdAsync(
                "oranauto",
                "vanwezel",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreatePreset("vanwezel"));

        var sut = new ReportPresetProvider(
            presets.Object,
            assignments.Object);

        var result = await sut.GetPresetAsync(
            "oranauto",
            "delivery-note",
            "customer",
            "000762");

        result.Should().NotBeNull();
        result!.Id.Should().Be("vanwezel");

        assignments.Verify(
            x => x.GetDefaultAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetPresetAsync_ShouldFallbackToDefaultAssignment()
    {
        var presets = new Mock<IReportPresetRepository>();
        var assignments = new Mock<IReportPresetAssignmentRepository>();

        assignments
            .Setup(x => x.GetAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReportPresetAssignment?)null);

        assignments
            .Setup(x => x.GetDefaultAsync(
                "oranauto",
                "delivery-note",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReportPresetAssignment
            {
                ReportId = "delivery-note",
                PresetId = "default"
            });

        presets
            .Setup(x => x.GetByIdAsync(
                "oranauto",
                "default",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreatePreset("default"));

        var sut = new ReportPresetProvider(
            presets.Object,
            assignments.Object);

        var result = await sut.GetPresetAsync(
            "oranauto",
            "delivery-note",
            "customer",
            "missing");

        result!.Id.Should().Be("default");
    }

    private static ReportPreset CreatePreset(string id) =>
        new()
        {
            Id = id,
            Configuration = new ReportConfiguration
            {
                Id = id,
                ReportId = "delivery-note",
                Name = id
            },
            Theme = new ReportTheme()
        };
}
