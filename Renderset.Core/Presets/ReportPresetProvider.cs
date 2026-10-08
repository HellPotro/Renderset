using System.Text.Json;
using Renderset.Core.Persistence;

namespace Renderset.Core.Presets;

public sealed class ReportPresetProvider
    : IReportPresetProvider
{
    private readonly IReportPresetRepository _presetRepository;
    private readonly IReportPresetAssignmentRepository _assignmentRepository;

    public ReportPresetProvider(
        IReportPresetRepository presetRepository,
        IReportPresetAssignmentRepository assignmentRepository)
    {
        _presetRepository = presetRepository;
        _assignmentRepository = assignmentRepository;
    }

    public async Task<ReportPreset?> GetPresetAsync(
        string tenantId,
        string reportId,
        string? contextType = null,
        string? contextKey = null,
        CancellationToken cancellationToken = default)
    {
        ReportPresetAssignment? assignment = null;

        if (!string.IsNullOrWhiteSpace(contextType) &&
            !string.IsNullOrWhiteSpace(contextKey))
        {
            assignment =
                await _assignmentRepository.GetAsync(
                    tenantId,
                    reportId,
                    contextType,
                    contextKey,
                    cancellationToken);
        }

        assignment ??=
            await _assignmentRepository.GetDefaultAsync(
                tenantId,
                reportId,
                cancellationToken);

        if (assignment is null)
            return null;

        return await _presetRepository.GetByIdAsync(
            tenantId,
            assignment.PresetId,
            cancellationToken);
    }

    public async Task<ReportPreset?> GetPresetAsync(
        string tenantId,
        string reportId,
        string? contextType,
        string? contextKey,
        JsonElement data,
        CancellationToken cancellationToken = default)
    {
        // Contexto completo en la petición: manda, como siempre.
        if (!string.IsNullOrWhiteSpace(contextType) &&
            !string.IsNullOrWhiteSpace(contextKey))
        {
            return await GetPresetAsync(
                tenantId,
                reportId,
                contextType,
                contextKey,
                cancellationToken);
        }

        var fromData =
            await FindByDataAsync(
                tenantId,
                reportId,
                contextType,
                data,
                cancellationToken);

        if (fromData is not null)
        {
            var preset =
                await _presetRepository.GetByIdAsync(
                    tenantId,
                    fromData.PresetId,
                    cancellationToken);

            if (preset is not null)
                return preset;
        }

        return await GetPresetAsync(
            tenantId,
            reportId,
            contextType: null,
            contextKey: null,
            cancellationToken);
    }

    /// <summary>
    /// Assignment cuyo tipo de contexto es una ruta de los datos y cuya clave
    /// coincide con el valor de esa ruta en el documento. Con
    /// <paramref name="onlyType"/> (la petición trae el tipo pero no la
    /// clave) sólo se mira ese tipo.
    /// </summary>
    private async Task<ReportPresetAssignment?> FindByDataAsync(
        string tenantId,
        string reportId,
        string? onlyType,
        JsonElement data,
        CancellationToken cancellationToken)
    {
        if (data.ValueKind != JsonValueKind.Object)
            return null;

        var assignments =
            await _assignmentRepository.GetAllAsync(
                tenantId,
                reportId,
                cancellationToken);

        if (assignments is null || assignments.Count == 0)
            return null;

        var byType =
            assignments
                .Where(x =>
                    !string.IsNullOrWhiteSpace(x.ContextType) &&
                    !string.IsNullOrWhiteSpace(x.ContextKey))
                .Where(x =>
                    onlyType is null ||
                    string.Equals(x.ContextType, onlyType, StringComparison.OrdinalIgnoreCase))
                .GroupBy(x => x.ContextType!, StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var group in byType)
        {
            var value = ReadValue(data, group.Key);

            if (value is null)
                continue;

            var match =
                group.FirstOrDefault(x =>
                    string.Equals(x.ContextKey!.Trim(), value, StringComparison.OrdinalIgnoreCase));

            if (match is not null)
                return match;
        }

        return null;
    }

    /// <summary>
    /// Valor tal cual viene en el JSON: "000762" sigue siendo "000762" y un
    /// 762 numérico es "762".
    /// </summary>
    internal static string? ReadValue(
        JsonElement data,
        string path)
    {
        var current = data;

        foreach (var part in path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (current.ValueKind != JsonValueKind.Object)
                return null;

            var found = false;

            foreach (var property in current.EnumerateObject())
            {
                if (property.Name.Equals(part, StringComparison.OrdinalIgnoreCase))
                {
                    current = property.Value;
                    found = true;
                    break;
                }
            }

            if (!found)
                return null;
        }

        var text =
            current.ValueKind switch
            {
                JsonValueKind.String => current.GetString(),
                JsonValueKind.Number => current.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => null
            };

        return string.IsNullOrWhiteSpace(text)
            ? null
            : text.Trim();
    }
}