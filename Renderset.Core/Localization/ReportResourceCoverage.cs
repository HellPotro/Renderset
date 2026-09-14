namespace Renderset.Core.Localization;

public sealed class ReportResourceCoverage
{
    public required string Scope { get; set; }

    public required string Culture { get; set; }

    public int TotalKeys { get; set; }

    public int TranslatedKeys { get; set; }

    public int MachineKeys { get; set; }

    public int PendingKeys => TotalKeys - TranslatedKeys;

    public decimal Percentage =>
        TotalKeys == 0
            ? 0
            : Math.Round(
                TranslatedKeys * 100m / TotalKeys,
                1);
}
