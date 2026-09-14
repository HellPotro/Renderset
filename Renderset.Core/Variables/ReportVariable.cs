namespace Renderset.Core.Variables;

public sealed class ReportVariable
{
    public required string Key { get; set; }

    public string? Value { get; set; }

    public string? Description { get; set; }

    public ReportVariableType Type { get; set; } =
        ReportVariableType.Text;
}