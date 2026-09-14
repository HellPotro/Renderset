using Renderset.Core.Tenancy;

namespace Renderset.Web.Tenancy;

public sealed class ConfiguredCurrentTenant
    : ICurrentTenant
{
    public ConfiguredCurrentTenant(
        IConfiguration configuration)
    {
        Id = configuration["Tenant:Id"]
            ?? throw new InvalidOperationException(
                "Falta la configuración 'Tenant:Id'.");

        Name = configuration["Tenant:Name"];
    }

    public string Id { get; }

    public string? Name { get; }
}
