using FluentAssertions;
using Renderset.Core.Rendering;
using Renderset.Core.Sharing;
using Renderset.Core.Tenancy;

namespace Renderset.Tests.Core;

public sealed class BundleTokenTests
{
    [Fact]
    public void Generate_ShouldProduceUrlSafeTokensOfFixedLength()
    {
        var token = BundleToken.Generate();

        token.Should().HaveLength(BundleToken.TextLength);
        token.Should().MatchRegex("^[A-Za-z0-9_-]+$");
        BundleToken.IsWellFormed(token).Should().BeTrue();
    }

    [Fact]
    public void Generate_ShouldNotRepeat()
    {
        var tokens =
            Enumerable.Range(0, 1000)
                .Select(_ => BundleToken.Generate())
                .ToHashSet();

        tokens.Should().HaveCount(1000);
    }

    [Fact]
    public void Matches_ShouldAcceptOnlyTheOriginalToken()
    {
        var token = BundleToken.Generate();
        var hash = BundleToken.Hash(token);

        BundleToken.Matches(token, hash).Should().BeTrue();
        BundleToken.Matches(BundleToken.Generate(), hash).Should().BeFalse();
        BundleToken.Matches(token[..^1], hash).Should().BeFalse();
        BundleToken.Matches(token, null).Should().BeFalse();
    }
}


public sealed class TenantBrandingTests
{
    [Theory]
    [InlineData("#00285A", "#00285A")]
    [InlineData(" #fff ", "#fff")]
    [InlineData("red", null)]
    [InlineData("#00285A; } body { display:none", null)]
    [InlineData("", null)]
    public void CleanColor_ShouldOnlyAcceptHexColors(
        string input,
        string? expected)
    {
        TenantBranding.CleanColor(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("https://oranauto.com/logo.png", true)]
    [InlineData("http://intranet/logo.png", true)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("data:text/html;base64,PHNjcmlwdD4=", false)]
    [InlineData("/relative/logo.png", false)]
    public void CleanLogoUrl_ShouldOnlyAcceptAbsoluteHttpUrls(
        string input,
        bool accepted)
    {
        (TenantBranding.CleanLogoUrl(input) is not null)
            .Should()
            .Be(accepted);
    }

    [Fact]
    public void Create_ShouldFallBackToDefaults()
    {
        var branding =
            TenantBranding.Create(
                "oranauto",
                displayName: null,
                logoUrl: "no es una url",
                primaryColor: "azul",
                secondaryColor: null);

        branding.DisplayName.Should().Be("oranauto");
        branding.LogoUrl.Should().BeNull();
        branding.PrimaryColor.Should().Be(TenantBranding.DefaultPrimaryColor);
        branding.SecondaryColor.Should().Be(TenantBranding.DefaultSecondaryColor);
    }
}


public sealed class DocumentStoragePathTests
{
    [Fact]
    public void BuildPath_ShouldNotEscapeTheTenantFolder()
    {
        var path =
            DocumentStorageOptions.BuildPath(
                "../../etc",
                "abc123",
                "..\\..\\passwd",
                new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc));

        path.Should().NotContain("..");
        path.Split('/').Should().HaveCount(5);
        path.Should().StartWith("_._etc/2026/10/abc123/");
    }

    [Fact]
    public void BuildPath_ShouldKeepReadableFileNames()
    {
        DocumentStorageOptions.BuildPath(
                "oranauto",
                "abc123",
                "factura-F-2026-0412.html",
                new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc))
            .Should()
            .Be("oranauto/2026/10/abc123/factura-F-2026-0412.html");
    }
}


public sealed class DocumentBundleTextsTests
{
    [Theory]
    [InlineData("es-ES", "Documentos")]
    [InlineData("fr", "Documents")]
    [InlineData("de-AT", "Dokumente")]
    [InlineData("ja-JP", "Documents")]
    [InlineData(null, "Documents")]
    public void For_ShouldPickTheLanguageAndFallBackToEnglish(
        string? culture,
        string expected)
    {
        DocumentBundleTexts.For(culture).Documents.Should().Be(expected);
    }
}
