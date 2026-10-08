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
        bool firstRowIsHeader = true) =>
        Parse(text, out _, delimiter, firstRowIsHeader);

    /// <summary>
    /// Igual, y además dice qué cabeceras repetidas se han renombrado
    /// ("Direccion_2"), para avisar a quien pega los datos.
    /// </summary>
    public static TabularPayload Parse(
        string text,
        out IReadOnlyList<string> renamedHeaders,
        char? delimiter = null,
        bool firstRowIsHeader = true) =>
        Parse(text, out renamedHeaders, delimiter, firstRowIsHeader, inferTypes: true);

    /// <summary>
    /// Igual, pero todas las columnas como texto: para quien interpreta los
    /// valores él mismo.
    /// </summary>
    public static TabularPayload ParseAsText(
        string text,
        out IReadOnlyList<string> renamedHeaders,
        char? delimiter = null) =>
        Parse(text, out renamedHeaders, delimiter, firstRowIsHeader: true, inferTypes: false);

    private static TabularPayload Parse(
        string text,
        out IReadOnlyList<string> renamedHeaders,
        char? delimiter,
        bool firstRowIsHeader,
        bool inferTypes)
    {
        var payload = new TabularPayload();
        var renamed = new List<string>();
        renamedHeaders = renamed;

        if (string.IsNullOrWhiteSpace(text))
            return payload;

        var normalized =
            text.Replace("\r\n", "\n")
                .Replace('\r', '\n');

        var firstLine =
            normalized
                .Split('\n')
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

        if (firstLine is null)
            return payload;

        var separator = delimiter ?? DetectDelimiter(firstLine);

        var grid =
            ReadRecords(normalized, separator)
                .Where(x => x.Any(value => !string.IsNullOrWhiteSpace(value)))
                .ToList();

        if (grid.Count == 0)
            return payload;

        // Con cabecera se sabe cuántas columnas tiene una fila: las que
        // llegan cortas son celdas con saltos de línea que SSMS copia sin
        // comillas, y se vuelven a unir.
        if (firstRowIsHeader)
            grid = [grid[0], .. JoinBrokenRows(grid.Skip(1).ToList(), grid[0].Count)];

        return FromGrid(grid, renamed, firstRowIsHeader, inferTypes: inferTypes);
    }

    /// <summary>
    /// Tipos y valores a partir de una rejilla de celdas ya separadas. Es lo
    /// que comparten el texto pegado y los ficheros (<see cref="TabularFile"/>):
    /// un Excel llega como celdas, no como texto, pero el análisis de cada
    /// columna es el mismo.
    /// </summary>
    /// <param name="ambiguousDecimalSeparator">
    /// Separador decimal cuando la columna no lo deja claro ("1.234"). Nulo
    /// usa la cultura del servidor, que es lo correcto para texto pegado; un
    /// Excel escribe sus números siempre con punto.
    /// </param>
    /// <param name="inferTypes">
    /// Falso: todas las columnas como texto, tal cual. Para quien interpreta
    /// él mismo los valores (la importación del DeCA lee "09/10/2026" como
    /// fecha española, no como 10 de septiembre).
    /// </param>
    internal static TabularPayload FromGrid(
        List<List<string>> grid,
        List<string> renamed,
        bool firstRowIsHeader = true,
        char? ambiguousDecimalSeparator = null,
        bool inferTypes = true)
    {
        var payload = new TabularPayload();

        grid =
            grid
                .Where(x => x.Any(value => !string.IsNullOrWhiteSpace(value)))
                .ToList();

        if (grid.Count == 0)
            return payload;

        var width = grid.Max(x => x.Count);

        var headers =
            firstRowIsHeader
                ? UniqueHeaders(Normalize(grid[0], width), renamed)
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

            var analysis =
                inferTypes
                    ? ColumnAnalysis.Analyze(values, ambiguousDecimalSeparator)
                    : ColumnAnalysis.Text;

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

    /// <summary>
    /// Separa el texto en filas y celdas. Una celda entre comillas puede
    /// llevar separadores y saltos de línea (CSV de Excel). Las comillas sólo
    /// cuentan al principio de la celda: un 3" en medio de un texto es un
    /// carácter más y no se come el resto del texto.
    /// </summary>
    private static List<List<string>> ReadRecords(
        string text,
        char separator)
    {
        var records = new List<List<string>>();
        var record = new List<string>();
        var current = new StringBuilder();

        var inQuotes = false;
        var atFieldStart = true;

        for (var i = 0; i < text.Length; i++)
        {
            var character = text[i];

            if (inQuotes)
            {
                if (character == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }

                    continue;
                }

                current.Append(character);
                continue;
            }

            if (character == '"' && atFieldStart)
            {
                inQuotes = true;
                atFieldStart = false;
                continue;
            }

            if (character == separator)
            {
                record.Add(current.ToString());
                current.Clear();
                atFieldStart = true;

                continue;
            }

            if (character == '\n')
            {
                record.Add(current.ToString());
                current.Clear();
                records.Add(record);
                record = [];
                atFieldStart = true;

                continue;
            }

            current.Append(character);
            atFieldStart = false;
        }

        record.Add(current.ToString());
        records.Add(record);

        return records;
    }

    /// <summary>
    /// SSMS copia una celda con salto de línea tal cual: la fila llega
    /// partida en dos líneas, la primera con menos columnas que la cabecera
    /// y la segunda empezando por el resto de esa celda. Mientras una fila
    /// tenga menos columnas que la cabecera y la siguiente quepa, se unen:
    /// la última celda de una con la primera de la otra, con el salto.
    ///
    /// Si no encaja exacto (la suma se pasa de la cabecera) no se toca nada:
    /// mejor una fila corta visible que una fila inventada.
    /// </summary>
    private static List<List<string>> JoinBrokenRows(
        List<List<string>> rows,
        int width)
    {
        var result = new List<List<string>>(rows.Count);

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];

            while (row.Count < width &&
                   i + 1 < rows.Count &&
                   row.Count + rows[i + 1].Count - 1 <= width)
            {
                var next = rows[++i];

                row[^1] = row[^1] + "\n" + next[0];
                row.AddRange(next.Skip(1));
            }

            result.Add(row);
        }

        return result;
    }

    /// <summary>
    /// Una SELECT con dos columnas Direccion (cliente y transportista) da
    /// dos cabeceras iguales, y el mapping localiza las columnas por nombre.
    /// La segunda pasa a Direccion_2, la tercera a Direccion_3...
    /// </summary>
    private static List<string> UniqueHeaders(
        List<string> headers,
        List<string> renamed)
    {
        var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>(headers.Count);

        foreach (var header in headers)
        {
            var name = header.Trim();

            if (name.Length == 0)
            {
                result.Add(name);
                continue;
            }

            if (!seen.TryGetValue(name, out var count))
            {
                seen[name] = 1;
                result.Add(name);

                continue;
            }

            var candidate = name;

            do
            {
                count++;
                candidate = $"{name}_{count}";
            }
            while (seen.ContainsKey(candidate));

            seen[name] = count;
            seen[candidate] = 1;
            result.Add(candidate);
            renamed.Add(candidate);
        }

        return result;
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

        /// <summary>
        /// Texto sin interpretar (ver inferTypes en FromGrid).
        /// </summary>
        public static ColumnAnalysis Text { get; } =
            new(ReportDataType.String, '.');

        public ReportDataType Type { get; }

        public char DecimalSeparator { get; }

        public static ColumnAnalysis Analyze(
            IReadOnlyCollection<string> values,
            char? ambiguousDecimalSeparator = null)
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

            var separator = DetectDecimalSeparator(present, ambiguousDecimalSeparator);

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
        /// Si toda la columna es ambigua se usa el separador que diga quien
        /// llama (un Excel: punto) o, si no dice nada, la cultura del
        /// servidor, que es la mejor apuesta disponible.
        /// </summary>
        private static char DetectDecimalSeparator(
            IReadOnlyCollection<string> values,
            char? ambiguous)
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

            return ambiguous
                ?? CultureInfo.CurrentCulture
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

            // SSMS copia los nulos como el texto NULL: en el JSON tienen que
            // llegar como null, igual que cuando las filas vienen por API.
            if (string.Equals(trimmed, "NULL", StringComparison.Ordinal))
                return null;

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