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


public sealed class DocumentSharingSettingsTests
{
    [Fact]
    public void Validate_ShouldAcceptEmptySettings()
    {
        new DocumentSharingSettings()
            .Validate(365)
            .Should()
            .BeEmpty();
    }

    [Fact]
    public void Validate_ShouldReportEachInvalidField()
    {
        var errors =
            new DocumentSharingSettings
            {
                LogoUrl = "javascript:alert(1)",
                PrimaryColor = "azul",
                SecondaryColor = "#12",
                FooterText = new string('x', DocumentSharingSettings.MaxFooterTextLength + 1),
                DefaultExpirationDays = 400
            }
            .Validate(365);

        errors
            .Select(x => x.Path)
            .Should()
            .BeEquivalentTo(
                "logoUrl",
                "primaryColor",
                "secondaryColor",
                "footerText",
                "defaultExpirationDays");
    }

    [Fact]
    public void Normalized_ShouldTurnBlankTextsIntoNull()
    {
        var normalized =
            new DocumentSharingSettings
            {
                DefaultMessage = "   ",
                FooterText = "  Dudas: logistica@oranauto.com  "
            }
            .Normalized();

        normalized.DefaultMessage.Should().BeNull();
        normalized.FooterText.Should().Be("Dudas: logistica@oranauto.com");
    }

    [Fact]
    public void Preview_ShouldUseTheDefaultMessageAndTheTenantColors()
    {
        var view =
            DocumentBundlePreview.Build(
                "oranauto",
                new DocumentSharingSettings
                {
                    TenantName = "ORAN Auto",
                    PrimaryColor = "#AA0000",
                    DefaultMessage = "Adjuntamos la documentación."
                },
                DocumentBundleTexts.For("es-ES"),
                new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc),
                fallbackExpirationDays: 30);

        view.Message.Should().Be("Adjuntamos la documentación.");
        view.Branding.DisplayName.Should().Be("ORAN Auto");
        view.Branding.PrimaryColor.Should().Be("#AA0000");
        view.Documents.Should().HaveCount(3);
        view.ExpiresAtUtc.Should().Be(new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc));
    }
}


public sealed class KeysetCursorTests
{
    private static readonly DateTime T0 =
        new(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Encode_ShouldRoundTrip()
    {
        var cursor =
            new Renderset.Core.Paging.KeysetCursor
            {
                CreatedAtUtc = T0.AddTicks(1234567),
                SeenIds = ["a", "b"]
            };

        var decoded =
            Renderset.Core.Paging.KeysetCursor.TryDecode(cursor.Encode());

        decoded!.CreatedAtUtc.Should().Be(cursor.CreatedAtUtc);
        decoded.SeenIds.Should().Equal("a", "b");
    }

    [Theory]
    [InlineData("no-es-un-cursor")]
    [InlineData("e30")]
    [InlineData("!!!")]
    public void TryDecode_ShouldRejectGarbage(
        string value)
    {
        Renderset.Core.Paging.KeysetCursor.TryDecode(value).Should().BeNull();
    }

    [Fact]
    public void Page_ShouldOnlyReturnACursorWhenThereIsAnExtraRow()
    {
        var rows =
            Enumerable.Range(0, 3)
                .Select(i => (Id: $"d{i}", At: T0.AddMinutes(-i)))
                .ToList();

        var full =
            Renderset.Core.Paging.KeysetCursor.Page(rows, 3, x => x.At, x => x.Id);

        full.Items.Should().HaveCount(3);
        full.HasMore.Should().BeFalse();

        var partial =
            Renderset.Core.Paging.KeysetCursor.Page(rows, 2, x => x.At, x => x.Id);

        partial.Items.Should().HaveCount(2);

        var next =
            Renderset.Core.Paging.KeysetCursor.TryDecode(partial.NextCursor);

        next!.CreatedAtUtc.Should().Be(T0.AddMinutes(-1));
        next.SeenIds.Should().Equal("d1");
    }

    /// <summary>
    /// Dos filas con el mismo instante en el borde de la página: el cursor
    /// las recuerda para que la siguiente no las repita ni las pierda.
    /// </summary>
    [Fact]
    public void Page_ShouldRememberEveryIdSharingTheLastTimestamp()
    {
        var rows =
            new List<(string Id, DateTime At)>
            {
                ("a", T0),
                ("b", T0.AddMinutes(-1)),
                ("c", T0.AddMinutes(-1)),
                ("d", T0.AddMinutes(-2))
            };

        var page =
            Renderset.Core.Paging.KeysetCursor.Page(rows, 3, x => x.At, x => x.Id);

        Renderset.Core.Paging.KeysetCursor
            .TryDecode(page.NextCursor)!
            .SeenIds
            .Should()
            .BeEquivalentTo("b", "c");
    }
}
