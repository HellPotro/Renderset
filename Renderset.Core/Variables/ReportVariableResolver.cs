using System.Text.RegularExpressions;

namespace Renderset.Core.Variables;

public sealed class ReportVariableResolver
    : IReportVariableResolver
{
    private static readonly Regex VariableRegex = new(
        @"\{\{\s*(?<key>[^{}]+?)\s*\}\}",
        RegexOptions.Compiled);

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

        var dictionary =
            variables.ToDictionary(
                x => x.Key,
                x => x.Value ?? string.Empty,
                StringComparer.OrdinalIgnoreCase);

        return VariableRegex.Replace(
            input,
            match =>
            {
                var key =
                    match.Groups["key"]
                        .Value
                        .Trim();

                return dictionary.TryGetValue(
                    key,
                    out var value)
                        ? value
                        : match.Value;
            });
    }
}