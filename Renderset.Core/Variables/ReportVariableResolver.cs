namespace Renderset.Core.Variables;

public sealed class ReportVariableResolver
    : IReportVariableResolver
{
    private readonly IReportVariableValues _values;

    public ReportVariableResolver(
        IReportVariableValues values)
    {
        _values = values;
    }

    public async Task<string> ResolveAsync(
        string tenantId,
        string input,
        string? culture = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input;

        var values =
            await _values.GetAsync(
                tenantId,
                culture,
                cancellationToken);

        // Misma sustitución que usa el preview del diseñador: si el marcador
        // no tiene valor se deja tal cual, para que se vea qué falta.
        return ReportVariableTemplate.Apply(
            input,
            values);
    }
}
