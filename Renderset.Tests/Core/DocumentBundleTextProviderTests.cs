using FluentAssertions;
using Moq;
using Renderset.Core.Localization;
using Renderset.Core.Sharing;

namespace Renderset.Tests.Core;

public sealed class DocumentBundleTextProviderTests
{
    private readonly Mock<IReportResourceRepository> _resources = new();
    private readonly Mock<ITenantCultureRepository> _cultures = new();

    private DocumentBundleTextProvider CreateProvider() =>
        new(_resources.Object, _cultures.Object);

    private void GivenSharingResources(
        params ReportResource[] resources)
    {
        _resources
            .Setup(x => x.GetByScopeAsync(
                "oranauto",
                ReportResourceScope.Sharing,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(resources);
    }

    private static ReportResource Resource(
        string key,
        string culture,
        string? value) =>
        new()
        {
            Scope = ReportResourceScope.Sharing,
            Key = key,
            Culture = culture,
            Value = value
        };

    [Fact]
    public async Task GetAsync_ShouldUseTheTenantTextAndKeepBuiltInForTheRest()
    {
        GivenSharingResources(
            Resource(DocumentBundleTextKeys.Documents, "es-ES", "Tu documentación"));

        var texts =
            await CreateProvider().GetAsync("oranauto", "es-ES");

        texts.Documents.Should().Be("Tu documentación");
        texts.Print.Should().Be("Imprimir");
    }

    [Fact]
    public async Task GetAsync_ShouldFallBackFromRegionalToNeutralCulture()
    {
        GivenSharingResources(
            Resource(DocumentBundleTextKeys.Print, "fr", "Imprimer le document"));

        var texts =
            await CreateProvider().GetAsync("oranauto", "fr-BE");

        texts.Print.Should().Be("Imprimer le document");
        texts.Download.Should().Be("Télécharger");
    }

    [Fact]
    public async Task GetAsync_ShouldNotUseAnotherLanguageText()
    {
        // El español del tenant no debe colarse en la página de un cliente
        // francés: ahí manda el francés incorporado.
        GivenSharingResources(
            Resource(DocumentBundleTextKeys.Documents, "es-ES", "Tu documentación"));

        var texts =
            await CreateProvider().GetAsync("oranauto", "fr-FR");

        texts.Documents.Should().Be("Documents");
    }

    [Fact]
    public async Task GetAsync_ShouldTranslateLanguagesWithoutBuiltInTexts()
    {
        GivenSharingResources(
            Resource(DocumentBundleTextKeys.Documents, "ca-ES", "Documents compartits"));

        var texts =
            await CreateProvider().GetAsync("oranauto", "ca-ES");

        texts.Documents.Should().Be("Documents compartits");
        texts.Download.Should().Be("Download");
    }

    [Fact]
    public async Task SeedAsync_ShouldSeedBuiltInTextsAndLeaveGapsForOtherLanguages()
    {
        _cultures
            .Setup(x => x.GetAllAsync("oranauto", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new TenantCulture { Culture = "es-ES", DisplayName = "Español", IsDefault = true },
                new TenantCulture { Culture = "ca-ES", DisplayName = "Català" }
            });

        IReadOnlyCollection<ReportResource>? seeded = null;

        _resources
            .Setup(x => x.SeedMissingAsync(
                "oranauto",
                It.IsAny<IReadOnlyCollection<ReportResource>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, IReadOnlyCollection<ReportResource>, CancellationToken>(
                (_, resources, _) => seeded = resources)
            .Returns(Task.CompletedTask);

        await CreateProvider().SeedAsync("oranauto");

        seeded.Should().NotBeNull();
        seeded!.Should().HaveCount(DocumentBundleTextKeys.All.Count * 2);
        seeded.Should().OnlyContain(x => x.Scope == ReportResourceScope.Sharing);

        seeded.Single(x => x.Culture == "es-ES" && x.Key == DocumentBundleTextKeys.Print)
            .Value.Should().Be("Imprimir");

        seeded.Where(x => x.Culture == "ca-ES")
            .Should().OnlyContain(x => x.Value == null);
    }

    [Fact]
    public void FormatAvailableUntil_ShouldNotThrowWithABrokenTemplate()
    {
        var texts =
            DocumentBundleTexts.For(
                "es-ES",
                new Dictionary<string, string>
                {
                    [DocumentBundleTextKeys.AvailableUntil] = "Hasta el {fecha}"
                });

        var text =
            texts.FormatAvailableUntil(
                new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc));

        text.Should().StartWith("Hasta el {fecha}");
    }
}
