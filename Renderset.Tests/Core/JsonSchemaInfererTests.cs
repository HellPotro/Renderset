using System.Text.Json;
using FluentAssertions;
using Renderset.Core.Reports;
using Renderset.Core.Reports.Inference;

namespace Renderset.Tests.Core;

public sealed class JsonSchemaInfererTests
{
    [Fact]
    public void Infer_ShouldDetectPrimitiveAndDateTypes()
    {
        using var document = JsonDocument.Parse("""
        {
          "name": "ORAN",
          "quantity": 12.5,
          "active": true,
          "date": "2026-09-14",
          "createdAt": "2026-09-14T08:30:00Z"
        }
        """);

        var schema = JsonSchemaInferer.Infer(document.RootElement);

        schema.Fields.Single(x => x.Name == "name").Type.Should().Be(ReportDataType.String);
        schema.Fields.Single(x => x.Name == "quantity").Type.Should().Be(ReportDataType.Number);
        schema.Fields.Single(x => x.Name == "active").Type.Should().Be(ReportDataType.Boolean);
        schema.Fields.Single(x => x.Name == "date").Type.Should().Be(ReportDataType.Date);
        schema.Fields.Single(x => x.Name == "createdAt").Type.Should().Be(ReportDataType.DateTime);
    }

    [Fact]
    public void Infer_ShouldMarkMissingArrayPropertiesAsNullable()
    {
        using var document = JsonDocument.Parse("""
        {
          "lines": [
            { "reference": "A", "observations": "Ok" },
            { "reference": "B" }
          ]
        }
        """);

        var schema = JsonSchemaInferer.Infer(document.RootElement);
        var lines = schema.Fields.Single(x => x.Name == "lines");

        lines.Type.Should().Be(ReportDataType.Array);
        lines.Children.Single(x => x.Name == "reference").Nullable.Should().BeFalse();
        lines.Children.Single(x => x.Name == "observations").Nullable.Should().BeTrue();
    }

    [Fact]
    public void Infer_ShouldRejectNonObjectRoot()
    {
        using var document = JsonDocument.Parse("[1,2,3]");

        var action = () => JsonSchemaInferer.Infer(document.RootElement);

        action.Should().Throw<ArgumentException>();
    }
}
