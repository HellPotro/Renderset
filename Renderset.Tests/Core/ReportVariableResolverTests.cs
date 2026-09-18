using FluentAssertions;
using Moq;
using Renderset.Core.Variables;

namespace Renderset.Tests.Core;

public sealed class ReportVariableResolverTests
{
    [Fact]
    public async Task ResolveAsync_ShouldReplaceVariablesCaseInsensitively()
    {
        var values = new Mock<IReportVariableValues>();

        values
            .Setup(x => x.GetAsync(
                "oranauto",
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase)
            {
                ["company.name"] = "Talleres ORAN S.L.U."
            });

        var sut = new ReportVariableResolver(values.Object);

        var result = await sut.ResolveAsync(
            "oranauto",
            "Empresa: {{ COMPANY.NAME }}");

        result.Should().Be("Empresa: Talleres ORAN S.L.U.");
    }

    [Fact]
    public async Task ResolveAsync_ShouldKeepUnknownVariablesUntouched()
    {
        var values = new Mock<IReportVariableValues>();

        values
            .Setup(x => x.GetAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string>());

        var sut = new ReportVariableResolver(values.Object);

        var result = await sut.ResolveAsync(
            "oranauto",
            "{{company.unknown}}");

        result.Should().Be("{{company.unknown}}");
    }

    /// <summary>
    /// La cultura viaja hasta el proveedor de valores: sin esto, un bloque
    /// pedido en inglés se resolvería con los textos del idioma por defecto.
    /// </summary>
    [Fact]
    public async Task ResolveAsync_ShouldForwardTheRequestedCulture()
    {
        var values = new Mock<IReportVariableValues>();

        values
            .Setup(x => x.GetAsync(
                It.IsAny<string>(),
                "en-GB",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase)
            {
                ["company.claim"] = "Precision stamping"
            });

        var sut = new ReportVariableResolver(values.Object);

        var result = await sut.ResolveAsync(
            "oranauto",
            "{{company.claim}}",
            "en-GB");

        result.Should().Be("Precision stamping");
    }
}

public sealed class ReportVariableTemplateTokenTests
{
    [Theory]
    [InlineData("{{todoslosderechos}}", true)]
    [InlineData("  {{ company.name }}  ", true)]
    [InlineData("{{a}} {{b}}", true)]
    [InlineData("© 2026 {{company.name}}. Todos los derechos.", false)]
    [InlineData("Texto sin variables", false)]
    [InlineData("", false)]
    public void IsOnlyTokens_ShouldDetectTextWithoutLanguage(
        string text,
        bool expected)
    {
        ReportVariableTemplate
            .IsOnlyTokens(text)
            .Should()
            .Be(expected);
    }

    [Fact]
    public void ExtractTokens_ShouldNormalizeKeys()
    {
        var tokens = ReportVariableTemplate.ExtractTokens(
            "{{ Company.Name }} y {{company.cif}} y {{COMPANY.NAME}}");

        tokens.Should().HaveCount(2);
        tokens.Should().Contain("company.cif");
    }
}
