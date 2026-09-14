using System.Text.Json;
using FluentAssertions;
using Renderset.Core.Data;

namespace Renderset.Tests.Core;

public sealed class JsonReportDataAccessorTests
{
    private readonly JsonReportDataAccessor _sut = new();

    [Fact]
    public void GetValue_ShouldResolveDottedPathCaseInsensitively()
    {
        using var document = JsonDocument.Parse("""
        {
          "Customer": {
            "Name": "N.V. Van Wezel"
          }
        }
        """);

        var value = _sut.GetValue(
            document.RootElement,
            "customer.name");

        value.Should().Be("N.V. Van Wezel");
    }

    [Fact]
    public void GetCollection_ShouldReturnClonedArrayItems()
    {
        using var document = JsonDocument.Parse("""
        {
          "lines": [
            { "reference": "A" },
            { "reference": "B" }
          ]
        }
        """);

        var items = _sut.GetCollection(
            document.RootElement,
            "lines");

        items.Should().HaveCount(2);
        items[0].GetProperty("reference").GetString().Should().Be("A");
    }

    [Fact]
    public void GetValue_ShouldReturnNullForUnknownPath()
    {
        using var document = JsonDocument.Parse("{\"name\":\"ORAN\"}");

        _sut.GetValue(document.RootElement, "missing")
            .Should().BeNull();
    }
}
