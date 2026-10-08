using System.Globalization;
using Renderset.Core.Definitions;

namespace Renderset.Core.Rendering;

/// <summary>
/// Cómo se pinta un valor según el tipo del campo o columna. Lo comparten
/// los campos, las tablas y sus totales, así que cambiar el tipo en el
/// diseñador afecta igual a todos.
/// </summary>
public static class ReportValueFormatter
{
    public static string Format(
        object? value,
        ReportFieldType type,
        CultureInfo? culture = null)
    {
        if (value is null)
            return string.Empty;

        culture ??= CultureInfo.CurrentCulture;

        switch (type)
        {
            case ReportFieldType.Integer when TryNumber(value, out var integer):
                return integer.ToString("N0", culture);

            case ReportFieldType.Number when TryNumber(value, out var number):
                return number.ToString("N2", culture);

            case ReportFieldType.Currency when TryNumber(value, out var amount):
                return amount.ToString("N2", culture);

            case ReportFieldType.Percentage when TryNumber(value, out var percentage):
                return $"{percentage.ToString("N2", culture)} %";

            case ReportFieldType.Date when TryDate(value, out var date):
                return date.ToString("dd/MM/yyyy", culture);

            case ReportFieldType.Boolean when value is bool boolean:
                return boolean ? "Sí" : "No";
        }

        return AsText(value);
    }

    /// <summary>
    /// Tipo Texto: el valor tal cual, sin miles ni decimales del idioma. Un
    /// código numérico (3116) sale 3116 y no 3.116,00.
    /// </summary>
    public static string AsText(
        object value) =>
        value switch
        {
            decimal number => number.ToString(CultureInfo.InvariantCulture),
            double number => number.ToString(CultureInfo.InvariantCulture),
            long number => number.ToString(CultureInfo.InvariantCulture),
            int number => number.ToString(CultureInfo.InvariantCulture),
            DateTime date when date.TimeOfDay == TimeSpan.Zero
                => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            DateTime date
                => date.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
            bool boolean => boolean ? "Sí" : "No",
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
        };

    /// <summary>
    /// Los tipos numéricos aceptan también texto con punto decimal: un
    /// campo que llega como "29.12" y se marca como número se pinta como
    /// número.
    /// </summary>
    public static bool TryNumber(
        object? value,
        out decimal number)
    {
        switch (value)
        {
            case decimal d:
                number = d;
                return true;

            case long l:
                number = l;
                return true;

            case int i:
                number = i;
                return true;

            case double d when !double.IsNaN(d) && !double.IsInfinity(d):
                number = (decimal)d;
                return true;

            case string text:
                return decimal.TryParse(
                    text.Trim(),
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out number);

            default:
                number = 0;
                return false;
        }
    }

    private static bool TryDate(
        object value,
        out DateTime date)
    {
        switch (value)
        {
            case DateTime d:
                date = d;
                return true;

            case DateTimeOffset offset:
                date = offset.DateTime;
                return true;

            case string text:
                return DateTime.TryParse(
                    text,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out date);

            default:
                date = default;
                return false;
        }
    }
}
