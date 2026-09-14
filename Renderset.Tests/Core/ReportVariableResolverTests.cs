using FluentAssertions;
using Moq;
using Renderset.Core.Variables;

namespace Renderset.Tests.Core;

public sealed class ReportVariableResolverTests
{
    [Fact]
    public async Task ResolveAsync_ShouldReplaceVariablesCaseInsensitively()
    {
        var repository = new Mock<IReportVariableRepository>();

        repository
            .Setup(x => x.GetAllAsync(
                "oranauto",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ReportVariable
                {
                    Key = "company.name",
                    Value = "Talleres ORAN S.L.U."
                }
            ]);

        var sut = new ReportVariableResolver(repository.Object);

        var result = await sut.ResolveAsync(
            "oranauto",
            "Empresa: {{ COMPANY.NAME }}");

        result.Should().Be("Empresa: Talleres ORAN S.L.U.");
    }

    [Fact]
    public async Task ResolveAsync_ShouldKeepUnknownVariablesUntouched()
    {
        var repository = new Mock<IReportVariableRepository>();

        repository
            .Setup(x => x.GetAllAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var sut = new ReportVariableResolver(repository.Object);

        var result = await sut.ResolveAsync(
            "oranauto",
            "{{company.unknown}}");

        result.Should().Be("{{company.unknown}}");
    }
}
