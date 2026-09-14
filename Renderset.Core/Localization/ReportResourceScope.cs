namespace Renderset.Core.Localization;

public static class ReportResourceScope
{
    /// <summary>
    /// Ámbito del diccionario común del tenant. Se usa un centinela en lugar
    /// de null porque la columna forma parte de la clave primaria.
    /// </summary>
    public const string Global = "*";

    public static string ForReport(
        string reportId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportId);

        return reportId;
    }

    public static string ForPreset(
        string presetId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(presetId);

        return $"preset:{presetId}";
    }

    public static bool IsPreset(
        string scope)
    {
        return scope.StartsWith(
            "preset:",
            StringComparison.OrdinalIgnoreCase);
    }
}
