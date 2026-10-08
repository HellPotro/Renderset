using Renderset.Core.Definitions;

namespace Renderset.Core.Rendering;

/// <summary>
/// Cálculo de la fila de totales de una tabla.
/// </summary>
public static class ReportTableTotals
{
    /// <summary>
    /// Nulo si no hay nada que calcular (todo vacío o sin números). Count
    /// cuenta los valores no vacíos, de cualquier tipo.
    /// </summary>
    public static decimal? Compute(
        IEnumerable<object?> values,
        ReportColumnTotal total)
    {
        if (total == ReportColumnTotal.None)
            return null;

        var present =
            values
                .Where(x => x is not null && !(x is string text && string.IsNullOrWhiteSpace(text)))
                .ToList();

        if (total == ReportColumnTotal.Count)
            return present.Count;

        var numbers =
            present
                .Select(x => ReportValueFormatter.TryNumber(x, out var number) ? number : (decimal?)null)
                .Where(x => x is not null)
                .Select(x => x!.Value)
                .ToList();

        if (numbers.Count == 0)
            return null;

        return total switch
        {
            ReportColumnTotal.Sum => numbers.Sum(),
            ReportColumnTotal.Average => numbers.Average(),
            ReportColumnTotal.Min => numbers.Min(),
            ReportColumnTotal.Max => numbers.Max(),
            _ => null
        };
    }

    /// <summary>
    /// El recuento siempre es un entero; el resto se pinta con el tipo de
    /// la columna, y si la columna es de texto (un código que se suma, raro
    /// pero posible), como número.
    /// </summary>
    public static string Format(
        decimal? value,
        ReportColumnTotal total,
        ReportFieldType columnType)
    {
        if (value is null)
            return string.Empty;

        var type =
            total == ReportColumnTotal.Count
                ? ReportFieldType.Integer
                : columnType switch
                {
                    ReportFieldType.Integer
                        or ReportFieldType.Number
                        or ReportFieldType.Currency
                        or ReportFieldType.Percentage => columnType,
                    _ => ReportFieldType.Number
                };

        // La media de enteros rara vez es entera: se enseña con decimales.
        if (total == ReportColumnTotal.Average && type == ReportFieldType.Integer)
            type = ReportFieldType.Number;

        return ReportValueFormatter.Format(value.Value, type);
    }
}
