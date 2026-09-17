using FluentAssertions;
using Renderset.Core.Definitions;
using Renderset.Core.Resolved;
using Renderset.Core.Services;

namespace Renderset.Tests.Core;

public sealed class ReportBodyLayoutTests
{
    [Fact]
    public void Apply_WithoutConfiguration_ShouldPutEveryItemInItsOwnRow()
    {
        var items = Items("a", "b", "c");

        ReportBodyLayout.Apply(items, null);

        items.Select(x => x.Row).Should().OnlyHaveUniqueItems();
        items.Should().OnlyContain(x => x.Span == ReportBodyLayout.Columns);
    }

    [Fact]
    public void Apply_WithSameRow_ShouldShareRowAndSplitWidth()
    {
        var items = Items("a", "b");

        ReportBodyLayout.Apply(
            items,
            [
                Setting("a"),
                Setting("b", sameRow: true)
            ]);

        items[0].Row.Should().Be(items[1].Row);
        items[0].Span.Should().Be(6);
        items[1].Span.Should().Be(6);
    }

    [Fact]
    public void Apply_WithExplicitSpan_ShouldRespectIt()
    {
        var items = Items("a", "b");

        ReportBodyLayout.Apply(
            items,
            [
                Setting("a", span: 8),
                Setting("b", sameRow: true, span: 4)
            ]);

        items[0].Span.Should().Be(8);
        items[1].Span.Should().Be(4);
    }

    [Fact]
    public void Apply_WithThreeInARow_ShouldFillTheRow()
    {
        var items = Items("a", "b", "c");

        ReportBodyLayout.Apply(
            items,
            [
                Setting("a"),
                Setting("b", sameRow: true),
                Setting("c", sameRow: true)
            ]);

        items.Should().OnlyContain(x => x.Row == 1);
        items.Sum(x => x.Span).Should().Be(ReportBodyLayout.Columns);
    }

    /// <summary>
    /// Pasa al borrar el elemento que tenía encima: la bandera se queda
    /// marcada y el primero no tiene con quién compartir fila.
    /// </summary>
    [Fact]
    public void Apply_WhenFirstItemContinuesARow_ShouldIgnoreTheFlag()
    {
        var items = Items("a", "b");

        ReportBodyLayout.Apply(
            items,
            [
                Setting("a", sameRow: true),
                Setting("b")
            ]);

        items[0].Row.Should().NotBe(items[1].Row);
        items[0].Span.Should().Be(ReportBodyLayout.Columns);
    }

    [Fact]
    public void Apply_WithUnknownItems_ShouldFallBackToOwnRow()
    {
        var items = Items("a", "huerfano");

        ReportBodyLayout.Apply(
            items,
            [
                Setting("a")
            ]);

        items[1].Row.Should().NotBe(items[0].Row);
        items[1].Span.Should().Be(ReportBodyLayout.Columns);
    }

    [Fact]
    public void BuildRows_ShouldGroupConsecutiveFlags()
    {
        var rows = ReportBodyLayout.BuildRows(
            [null, true, null, true, true]);

        rows.Should().HaveCount(2);
        rows[0].Should().Equal(0, 1);
        rows[1].Should().Equal(2, 3, 4);
    }

    [Theory]
    [InlineData(1, 12)]
    [InlineData(2, 6)]
    [InlineData(3, 4)]
    [InlineData(4, 3)]
    public void DistributeSpans_ShouldSplitEvenly(
        int count,
        int expected)
    {
        var spans = ReportBodyLayout.DistributeSpans(
            new int?[count]);

        spans.Should().OnlyContain(x => x == expected);
        spans.Sum().Should().Be(ReportBodyLayout.Columns);
    }

    [Fact]
    public void DistributeSpans_WithUnevenCount_ShouldStillFillTheRow()
    {
        var spans = ReportBodyLayout.DistributeSpans(
            new int?[5]);

        spans.Should().Equal(3, 3, 2, 2, 2);
    }

    private static List<ResolvedReportBodyItem> Items(
        params string[] ids) =>
        ids
            .Select((id, index) => new ResolvedReportBodyItem
            {
                Id = id,
                Type = ReportBodyItemType.Section,
                Order = (index + 1) * 10
            })
            .ToList();

    private static ReportBodyItemConfiguration Setting(
        string id,
        bool? sameRow = null,
        int? span = null) =>
        new()
        {
            Id = id,
            Type = ReportBodyItemType.Section,
            SameRow = sameRow,
            Span = span
        };
}
