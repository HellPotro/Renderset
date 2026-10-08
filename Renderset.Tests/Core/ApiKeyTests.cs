using FluentAssertions;
using Renderset.Core.Security;

namespace Renderset.Tests.Core;

public sealed class ApiKeyFormatTests
{
    [Fact]
    public void Generate_ShouldProduceRecognizableWellFormedKeys()
    {
        var key = ApiKeyFormat.Generate();

        key.Should().StartWith("rs_live_");
        ApiKeyFormat.IsWellFormed(key).Should().BeTrue();
        ApiKeyFormat.VisiblePrefix(key).Should().HaveLength(ApiKeyFormat.VisiblePrefixLength);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("rs_live_corta")]
    [InlineData("some_other_api_key_AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("rs_live_AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA!")]
    public void IsWellFormed_ShouldRejectAnythingElse(
        string? key)
    {
        ApiKeyFormat.IsWellFormed(key).Should().BeFalse();
    }
}


public sealed class ApiKeyServiceTests
{
    private static readonly DateTime Now =
        new(2026, 10, 5, 10, 0, 0, DateTimeKind.Utc);

    private readonly InMemoryApiKeyRepository _repository = new();
    private readonly FixedTime _time = new(Now);

    private ApiKeyService CreateSut() =>
        new(_repository, _time);

    [Fact]
    public async Task CreateAsync_ShouldStoreOnlyTheHash()
    {
        var created = await CreateSut().CreateAsync("oranauto", "ERP", null);

        var stored = _repository.Keys.Single();

        stored.Hash.Should().Equal(ApiKeyFormat.Hash(created.PlainText));
        stored.Prefix.Should().Be(ApiKeyFormat.VisiblePrefix(created.PlainText));
        created.PlainText.Should().NotContain(stored.Prefix + "…");
    }

    [Fact]
    public async Task AuthenticateAsync_ShouldReturnTheKeyAndItsTenant()
    {
        var sut = CreateSut();
        var created = await sut.CreateAsync("oranauto", "ERP", null);

        var key = await sut.AuthenticateAsync(created.PlainText);

        key!.TenantId.Should().Be("oranauto");
    }

    [Fact]
    public async Task AuthenticateAsync_ShouldRejectRevokedKeys()
    {
        var sut = CreateSut();
        var created = await sut.CreateAsync("oranauto", "ERP", null);

        await sut.RevokeAsync("oranauto", created.Key.Id);

        (await sut.AuthenticateAsync(created.PlainText)).Should().BeNull();
    }

    [Fact]
    public async Task AuthenticateAsync_ShouldRejectExpiredKeys()
    {
        var sut = CreateSut();
        var created = await sut.CreateAsync("oranauto", "Pruebas", Now.AddDays(1));

        _time.Now = Now.AddDays(1);

        (await sut.AuthenticateAsync(created.PlainText)).Should().BeNull();
    }

    [Fact]
    public async Task AuthenticateAsync_ShouldNotWriteTheLastUseOnEveryRequest()
    {
        var sut = CreateSut();
        var created = await sut.CreateAsync("oranauto", "ERP", null);

        await sut.AuthenticateAsync(created.PlainText);

        _time.Now = Now.AddMinutes(1);
        await sut.AuthenticateAsync(created.PlainText);

        _repository.Touches.Should().Be(1);

        _time.Now = Now.AddMinutes(10);
        await sut.AuthenticateAsync(created.PlainText);

        _repository.Touches.Should().Be(2);
    }

    [Fact]
    public async Task RevokeAsync_ShouldNotTouchKeysOfAnotherTenant()
    {
        var sut = CreateSut();
        var created = await sut.CreateAsync("oranauto", "ERP", null);

        (await sut.RevokeAsync("otro", created.Key.Id)).Should().BeFalse();
        (await sut.AuthenticateAsync(created.PlainText)).Should().NotBeNull();
    }


    private sealed class FixedTime(DateTime now)
        : TimeProvider
    {
        public DateTime Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() =>
            new(Now);
    }


    private sealed class InMemoryApiKeyRepository
        : IApiKeyRepository
    {
        public List<ApiKey> Keys { get; } = [];

        public int Touches { get; private set; }

        public Task<ApiKey?> FindByHashAsync(
            byte[] hash,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Keys.FirstOrDefault(x => x.Hash.SequenceEqual(hash)));

        public Task<IReadOnlyList<ApiKey>> ListAsync(
            string tenantId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ApiKey>>(
                Keys.Where(x => x.TenantId == tenantId).ToList());

        public Task CreateAsync(
            ApiKey key,
            CancellationToken cancellationToken = default)
        {
            Keys.Add(key);
            return Task.CompletedTask;
        }

        public Task<bool> RevokeAsync(
            string tenantId,
            Guid keyId,
            DateTime revokedAtUtc,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                Replace(
                    keyId,
                    x => x.TenantId == tenantId,
                    x => Copy(x, revokedAtUtc: revokedAtUtc)));

        public Task TouchAsync(
            Guid keyId,
            DateTime usedAtUtc,
            CancellationToken cancellationToken = default)
        {
            Touches++;
            Replace(keyId, _ => true, x => Copy(x, lastUsedAtUtc: usedAtUtc));
            return Task.CompletedTask;
        }

        private bool Replace(
            Guid id,
            Func<ApiKey, bool> condition,
            Func<ApiKey, ApiKey> change)
        {
            var index = Keys.FindIndex(x => x.Id == id && condition(x));

            if (index < 0)
                return false;

            Keys[index] = change(Keys[index]);
            return true;
        }

        private static ApiKey Copy(
            ApiKey source,
            DateTime? revokedAtUtc = null,
            DateTime? lastUsedAtUtc = null) =>
            new()
            {
                Id = source.Id,
                TenantId = source.TenantId,
                Name = source.Name,
                Prefix = source.Prefix,
                Hash = source.Hash,
                CreatedAtUtc = source.CreatedAtUtc,
                ExpiresAtUtc = source.ExpiresAtUtc,
                RevokedAtUtc = revokedAtUtc ?? source.RevokedAtUtc,
                LastUsedAtUtc = lastUsedAtUtc ?? source.LastUsedAtUtc
            };
    }
}


public sealed class RendersetCallerTests
{
    [Theory]
    [InlineData("oranauto", true)]
    [InlineData("ORANAUTO", true)]
    [InlineData("otro", false)]
    [InlineData(null, false)]
    public void TenantCaller_ShouldOnlyAccessItsOwnTenant(
        string? routeTenant,
        bool expected)
    {
        new RendersetCaller
        {
            Kind = RendersetCallerKind.Tenant,
            TenantId = "oranauto",
            Name = "ERP"
        }
            .CanAccessTenant(routeTenant)
            .Should()
            .Be(expected);
    }

    [Fact]
    public void ServiceCaller_ShouldAccessAnyTenant()
    {
        new RendersetCaller
        {
            Kind = RendersetCallerKind.Service,
            Name = "web"
        }
            .CanAccessTenant("cualquiera")
            .Should()
            .BeTrue();
    }
}
