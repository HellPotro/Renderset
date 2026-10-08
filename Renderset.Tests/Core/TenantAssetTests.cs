using FluentAssertions;
using Renderset.Core.Tenancy;

namespace Renderset.Tests.Core;

public sealed class TenantAssetTests
{
    [Fact]
    public void DetectContentType_ShouldLookAtTheContentNotTheName()
    {
        TenantAssetRules.DetectContentType([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0])
            .Should().Be("image/png");

        TenantAssetRules.DetectContentType([0xFF, 0xD8, 0xFF, 0xE0])
            .Should().Be("image/jpeg");

        TenantAssetRules.DetectContentType("GIF89a......"u8)
            .Should().Be("image/gif");

        TenantAssetRules.DetectContentType("RIFF\0\0\0\0WEBPVP8 "u8)
            .Should().Be("image/webp");
    }

    [Fact]
    public void DetectContentType_ShouldRejectSvgAndAnythingElse()
    {
        TenantAssetRules.DetectContentType("<svg xmlns=\"http://www.w3.org/2000/svg\"><script/></svg>"u8)
            .Should().BeNull();

        TenantAssetRules.DetectContentType("%PDF-1.7"u8)
            .Should().BeNull();
    }

    [Fact]
    public void IdFor_ShouldBeTheSameForTheSameImage()
    {
        var a = TenantAssetRules.IdFor([1, 2, 3]);

        a.Should().HaveLength(32);
        a.Should().Be(TenantAssetRules.IdFor([1, 2, 3]));
        a.Should().NotBe(TenantAssetRules.IdFor([1, 2, 4]));
    }

    [Theory]
    [InlineData("0123456789abcdef0123456789ABCDEF.png", "0123456789abcdef0123456789abcdef")]
    [InlineData("0123456789abcdef0123456789abcdef", "0123456789abcdef0123456789abcdef")]
    [InlineData("../../web.config", null)]
    [InlineData("0123456789abcdef.png", null)]
    [InlineData("", null)]
    public void ParsePublicFileName_ShouldOnlyAcceptHashes(
        string file,
        string? expected)
    {
        TenantAssetRules.ParsePublicFileName(file).Should().Be(expected);
    }
}
