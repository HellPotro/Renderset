namespace Renderset.Core.Tenancy;

public interface ICurrentTenant
{
    string Id { get; }

    string? Name { get; }
}
