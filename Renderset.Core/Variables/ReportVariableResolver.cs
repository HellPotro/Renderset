namespace Renderset.Core.Variables;

public sealed class ReportVariableResolver
    : IReportVariableResolver
{
    private readonly IReportVariableRepository _repository;

    public ReportVariableResolver(
        IReportVariableRepository repository)
    {
        _repository = repository;
    }

    public async Task<string> ResolveAsync(
        string tenantId,
        string input,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input;

        var variables =
            await _repository.GetAllAsync(
                tenantId,
                cancellationToken);

        // Misma sustitución que usa el preview del diseñador: si el marcador
        // no tiene valor se deja tal cual, para que se vea qué falta.
        return ReportVariableTemplate.Apply(
            input,
            ReportVariableTemplate.ToDictionary(variables));
    }
}