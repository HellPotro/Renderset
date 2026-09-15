using Renderset.Core.Reports;
using System.Globalization;
using System.Text;

namespace Renderset.Core.DataSources;

/// <summary>
/// Convierte texto tabular pegado (TSV, CSV, punto y coma) en un
/// <see cref="TabularPayload"/>.
///
/// Está pensado sobre todo para pegar directamente desde la rejilla de
/// resultados de SQL Server Management Studio, que copia con tabuladores y
/// una fila de cabecera.
///
/// Sirve además como base del futuro proveedor de CSV: leer un fichero es lo
/// mismo que leer un texto pegado.
/// </summary>
public static class DelimitedTextParser
{
    public static TabularPayload Parse(
        string text,
        char? delimiter = null,
        bool firstRowIsHeader = true)
    {
        var payload = new TabularPayload();

        if (string.IsNullOrWhiteSpace(text))
            return payload;

        var lines =
            text.Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Split('\n')
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

        if (lines.Count == 0)
            return payload;

        var separator = delimiter ?? DetectDelimiter(lines[0]);

        var grid =
            lines
                .Select(line => SplitLine(line, separator))
                .ToList();

        var width = grid.Max(x => x.Count);

        var headers =
            firstRowIsHeader
                ? Normalize(grid[0], width)
                : Enumerable
                    .Range(1, width)
                    .Select(i => $"Column{i}")
                    .ToList();

        var body =
            firstRowIsHeader
                ? grid.Skip(1).ToList()
                : grid;

        var rows =
            body
                .Select(row => Normalize(row, width))
                .ToList();

        // El análisis es por columna, no por celda: el separador decimal se
        // decide mirando todos los valores juntos.
        var analyses = new List<ColumnAnalysis>(width);

        for (var column = 0; column < width; column++)
        {
            var values =
                rows
                    .Select(row => row[column])
                    .ToList();

            var analysis = ColumnAnalysis.Analyze(values);

            analyses.Add(analysis);

            payload.Columns.Add(
                new TabularPayloadColumn
                {
                    Name = string.IsNullOrWhiteSpace(headers[column])
                        ? $"Column{column + 1}"
                        : headers[column].Trim(),

                    Type = analysis.Type,
                    Nullable = values.Any(string.IsNullOrWhiteSpace)
                });
        }

        foreach (var row in rows)
        {
            var values = new List<object?>(width);

            for (var column = 0; column < width; column++)
                values.Add(analyses[column].Convert(row[column]));

            payload.Rows.Add(values);
        }

        return payload;
    }

    /// <summary>
    /// SSMS copia con tabuladores; un CSV exportado de Excel en español usa
    /// punto y coma. Se elige el separador que produce más columnas en la
    /// primera línea.
    /// </summary>
    public static char DetectDelimiter(
        string firstLine)
    {
        var candidates = new[] { '\t', ';', ',', '|' };

        return candidates
            .OrderByDescending(x => firstLine.Count(c => c == x))
            .First();
    }

    public static ReportDataType InferType(
        IReadOnlyCollection<string> values) =>
        ColumnAnalysis.Analyze(values).Type;

    private static List<string> SplitLine(
        string line,
        char separator)
    {
        var values = new List<string>();
        var current = new StringBuilder();

        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var character = line[i];

            if (character == '"')
            {
                if (inQuotes &&
                    i + 1 < line.Length &&
                    line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;

                    continue;
                }

                inQuotes = !inQuotes;

                continue;
            }

            if (character == separator && !inQuotes)
            {
                values.Add(current.ToString());
                current.Clear();

                continue;
            }

            current.Append(character);
        }

        values.Add(current.ToString());

        return values;
    }

    private static List<string> Normalize(
        List<string> row,
        int width)
    {
        while (row.Count < width)
            row.Add(string.Empty);

        return row;
    }


    /// <summary>
    /// Análisis de una columna completa: tipo y, para los números, qué
    /// carácter es el separador decimal.
    ///
    /// Decidirlo celda a celda es lo que hacía que "148,24" acabara siendo
    /// 14824: en cultura invariante la coma es separador de miles y
    /// NumberStyles.Number la acepta sin rechistar. Mirando la columna entera
    /// se puede distinguir.
    /// </summary>
    internal sealed class ColumnAnalysis
    {
        private ColumnAnalysis(
            ReportDataType type,
            char decimalSeparator)
        {
            Type = type;
            DecimalSeparator = decimalSeparator;
        }

        public ReportDataType Type { get; }

        public char DecimalSeparator { get; }

        public static ColumnAnalysis Analyze(
            IReadOnlyCollection<string> values)
        {
            var present =
                values
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .ToList();

            if (present.Count == 0)
                return new ColumnAnalysis(ReportDataType.String, '.');

            if (present.All(IsBoolean))
                return new ColumnAnalysis(ReportDataType.Boolean, '.');

            var separator = DetectDecimalSeparator(present);

            if (present.All(x => IsNumber(x, separator)))
                return new ColumnAnalysis(ReportDataType.Number, separator);

            if (present.All(x => TryParseDate(x, out _, out var hasTime) && !hasTime))
                return new ColumnAnalysis(ReportDataType.Date, separator);

            if (present.All(x => TryParseDate(x, out _, out _)))
                return new ColumnAnalysis(ReportDataType.DateTime, separator);

            return new ColumnAnalysis(ReportDataType.String, separator);
        }

        /// <summary>
        /// Reglas, en orden:
        ///
        /// - Si un valor tiene punto y coma, el ÚLTIMO de los dos es el
        ///   decimal: "1.234,56" y "1,234.56" quedan resueltos.
        /// - Si sólo aparece uno y lo hace varias veces, es separador de
        ///   miles: "1.234.567".
        /// - Si sólo aparece uno, una vez, y no deja exactamente tres cifras
        ///   detrás, es decimal: "148,24" y "7412,5".
        /// - Si deja exactamente tres cifras ("1,234") es ambiguo y no vota.
        ///
        /// Si toda la columna es ambigua se usa la cultura del servidor, que
        /// es la mejor apuesta disponible.
        /// </summary>
        private static char DetectDecimalSeparator(
            IReadOnlyCollection<string> values)
        {
            var comma = 0;
            var dot = 0;

            foreach (var value in values)
            {
                var lastComma = value.LastIndexOf(',');
                var lastDot = value.LastIndexOf('.');

                if (lastComma >= 0 && lastDot >= 0)
                {
                    if (lastComma > lastDot)
                        comma++;
                    else
                        dot++;

                    continue;
                }

                if (lastComma >= 0)
                {
                    Vote(value, ',', lastComma, ref comma);
                    continue;
                }

                if (lastDot >= 0)
                    Vote(value, '.', lastDot, ref dot);
            }

            if (comma > dot)
                return ',';

            if (dot > comma)
                return '.';

            return CultureInfo.CurrentCulture
                .NumberFormat
                .NumberDecimalSeparator[0];
        }

        private static void Vote(
            string value,
            char character,
            int lastIndex,
            ref int votes)
        {
            if (value.Count(c => c == character) > 1)
                return;

            var digitsAfter = value.Length - lastIndex - 1;

            // Tres cifras detrás es ambiguo: "1,234" puede ser mil doscientos
            // treinta y cuatro o uno coma doscientos treinta y cuatro.
            if (digitsAfter == 3)
                return;

            votes++;
        }

        private static bool IsBoolean(
            string value) =>
            bool.TryParse(value, out _) ||
            value is "0" or "1";

        /// <summary>
        /// Dos reglas que protegen datos reales:
        ///
        /// - Un valor con ceros a la izquierda ("00156030") NO es un número:
        ///   es una referencia o un código, y convertirlo lo destroza.
        /// - Más de quince cifras tampoco: códigos de barras, IBANs,
        ///   identificadores. Pierden precisión.
        /// </summary>
        private static bool IsNumber(
            string value,
            char decimalSeparator)
        {
            if (value.Length > 1 &&
                value[0] == '0' &&
                value[1] != decimalSeparator)
            {
                return false;
            }

            if (value.Count(char.IsDigit) > 15)
                return false;

            return TryParseNumber(value, decimalSeparator, out _);
        }

        private static bool TryParseNumber(
            string value,
            char decimalSeparator,
            out decimal result)
        {
            var builder = new StringBuilder(value.Length);

            var seenSeparator = false;

            for (var i = 0; i < value.Length; i++)
            {
                var character = value[i];

                if (char.IsDigit(character))
                {
                    builder.Append(character);
                    continue;
                }

                if (character == decimalSeparator)
                {
                    // Dos separadores decimales no son un número.
                    if (seenSeparator)
                    {
                        result = 0;
                        return false;
                    }

                    seenSeparator = true;
                    builder.Append('.');

                    continue;
                }

                if ((character == '-' || character == '+') && i == 0)
                {
                    builder.Append(character);
                    continue;
                }

                // Cualquier otra cosa se trata como separador de miles y se
                // descarta: espacios, apóstrofos y el separador contrario.
                if (character is '.' or ',' or ' ' or '\u00a0' or '\'')
                    continue;

                result = 0;
                return false;
            }

            var cleaned = builder.ToString();

            if (cleaned.Length == 0 || cleaned is "-" or "+")
            {
                result = 0;
                return false;
            }

            // Ya normalizado a punto decimal y sin miles, así que la cultura
            // invariante es ahora sí la correcta.
            return decimal.TryParse(
                cleaned,
                NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out result);
        }

        private static bool TryParseDate(
            string value,
            out DateTime result,
            out bool hasTime)
        {
            hasTime = false;

            var parsed =
                DateTime.TryParse(
                    value,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out result)
                ||
                DateTime.TryParse(
                    value,
                    CultureInfo.CurrentCulture,
                    DateTimeStyles.None,
                    out result);

            if (!parsed)
                return false;

            hasTime =
                result.TimeOfDay != TimeSpan.Zero ||
                value.Contains(':');

            return true;
        }

        public object? Convert(
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var trimmed = value.Trim();

            switch (Type)
            {
                case ReportDataType.Boolean:
                    if (trimmed == "1")
                        return true;

                    if (trimmed == "0")
                        return false;

                    return bool.Parse(trimmed);

                case ReportDataType.Number:
                    return TryParseNumber(trimmed, DecimalSeparator, out var number)
                        ? number
                        : null;

                case ReportDataType.Date:
                case ReportDataType.DateTime:
                    return TryParseDate(trimmed, out var date, out _)
                        ? date
                        : null;

                default:
                    return trimmed;
            }
        }
    }
}