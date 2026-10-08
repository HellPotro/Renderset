using System.Security.Cryptography;
using FluentAssertions;
using Renderset.Core.Security;
using Renderset.Core.Tenancy;

namespace Renderset.Tests.Core;

public sealed class TenantTokenTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 6, 15, 0, 0, TimeSpan.Zero);

    private static (string Private, string Public) NewKeys()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        return (key.ExportPkcs8PrivateKeyPem(), key.ExportSubjectPublicKeyInfoPem());
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact]
    public void Token_ShouldCarryTenantUserAndRole()
    {
        var (priv, pub) = NewKeys();
        var time = new FixedTime(Now);

        using var signer = new TenantTokenSigner(priv, time);
        using var validator = new TenantTokenValidator(pub, time);

        var token = signer.Create("oranauto", "user-1", TenantRole.Admin);

        validator.TryValidate(token, out var claims, out var error).Should().BeTrue(error);

        claims!.TenantId.Should().Be("oranauto");
        claims.UserId.Should().Be("user-1");
        claims.Role.Should().Be(TenantRole.Admin);
    }

    [Fact]
    public void Token_ShouldExpire()
    {
        var (priv, pub) = NewKeys();
        var time = new FixedTime(Now);

        using var signer = new TenantTokenSigner(priv, time);
        using var validator = new TenantTokenValidator(pub, time);

        var token = signer.Create("oranauto", "user-1", TenantRole.Member);

        time.Now = Now.AddMinutes(10);

        validator.TryValidate(token, out _, out var error).Should().BeFalse();
        error.Should().Contain("caducado");
    }

    [Fact]
    public void Token_ShouldNotAcceptAChangedTenant()
    {
        var (priv, pub) = NewKeys();

        using var signer = new TenantTokenSigner(priv);
        using var validator = new TenantTokenValidator(pub);

        var token = signer.Create("oranauto", "user-1", TenantRole.Member);
        var parts = token.Split('.');

        // Mismo token con otro tenant en el contenido: la firma ya no cuadra.
        var payload = System.Text.Encoding.UTF8.GetString(
            Convert.FromBase64String(parts[1].Replace('-', '+').Replace('_', '/').PadRight((parts[1].Length + 3) / 4 * 4, '=')));

        var forged = payload.Replace("oranauto", "otro-tenant");
        var forgedPart = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(forged))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        validator.TryValidate($"{parts[0]}.{forgedPart}.{parts[2]}", out _, out _).Should().BeFalse();
    }

    [Fact]
    public void Token_ShouldNotValidateWithAnotherKey()
    {
        var (priv, _) = NewKeys();
        var (_, otherPub) = NewKeys();

        using var signer = new TenantTokenSigner(priv);
        using var validator = new TenantTokenValidator(otherPub);

        validator.TryValidate(signer.Create("oranauto", "u", TenantRole.Owner), out _, out _).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("v1.abc")]
    [InlineData("v2.abc.def")]
    [InlineData("v1.!!!.@@@")]
    public void Token_ShouldRejectGarbage(string? token)
    {
        var (_, pub) = NewKeys();
        using var validator = new TenantTokenValidator(pub);

        validator.TryValidate(token, out _, out _).Should().BeFalse();
    }

    [Theory]
    [InlineData("oranauto", true)]
    [InlineData("otro", false)]
    public void ServiceWithUserToken_ShouldOnlyAccessTheTokenTenant(string route, bool expected)
    {
        new RendersetCaller
            {
                Kind = RendersetCallerKind.Service,
                TenantId = "oranauto",
                UserId = "user-1",
                Role = TenantRole.Member,
                Name = "web · user-1"
            }
            .CanAccessTenant(route)
            .Should()
            .Be(expected);
    }

    [Fact]
    public void Roles_ShouldGateAdministration()
    {
        RendersetCaller Caller(RendersetCallerKind kind, string? tenant, TenantRole? role) =>
            new() { Kind = kind, TenantId = tenant, Role = role, Name = "x" };

        Caller(RendersetCallerKind.Service, "t", TenantRole.Member).HasRole(TenantRole.Admin).Should().BeFalse();
        Caller(RendersetCallerKind.Service, "t", TenantRole.Admin).HasRole(TenantRole.Admin).Should().BeTrue();
        Caller(RendersetCallerKind.Service, "t", TenantRole.Owner).HasRole(TenantRole.Admin).Should().BeTrue();
        Caller(RendersetCallerKind.Service, null, null).HasRole(TenantRole.Admin).Should().BeTrue();

        // Una API key de tenant (el ERP) nunca administra.
        Caller(RendersetCallerKind.Tenant, "t", null).HasRole(TenantRole.Admin).Should().BeFalse();
    }

    [Fact]
    public void Keys_ShouldAlsoBeAcceptedAsSingleLineBase64()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        var priv = Convert.ToBase64String(key.ExportPkcs8PrivateKey());
        var pub = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());

        using var signer = new TenantTokenSigner(priv);
        using var validator = new TenantTokenValidator(pub);

        validator.TryValidate(signer.Create("t", "u", TenantRole.Member), out _, out var error)
            .Should().BeTrue(error);
    }

    [Fact]
    public void Keys_ShouldRejectGarbageAndOtherCurves()
    {
        var garbage = () => new TenantTokenValidator("no-es-una-clave");
        garbage.Should().Throw<ArgumentException>();

        using var p384 = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        var wrongCurve = () => new TenantTokenValidator(p384.ExportSubjectPublicKeyInfoPem());
        wrongCurve.Should().Throw<ArgumentException>();
    }
}
