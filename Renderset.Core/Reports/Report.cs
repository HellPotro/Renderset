using Renderset.Core.Definitions;
using System.Text.Json;

namespace Renderset.Core.Reports;

public sealed class Report
{
    public required string Id { get; set; }

    public required string Name { get; set; }

    public int Version { get; set; } = 1;

    public required ReportDefinition Definition { get; set; }

    public required ReportDataSchema DataSchema { get; set; }

    public JsonElement? SampleData { get; set; }
}