namespace Renderset.Core.Variables;

public interface IReportVariableResolver
{
    Task<string> ResolveAsync(
        string tenantId,
        string input,
        CancellationToken cancellationToken = default);
}