using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;

namespace Renderset.Core.DataSources;

/// <summary>
/// Lector mínimo de .xlsx: hojas, celdas y valores. Nada de fórmulas,
/// estilos ni gráficos; sólo lo que hace falta para sacar una tabla.
///
/// Un .xlsx es un ZIP con XML dentro, así que basta con lo que ya trae .NET
/// y Core no gana ninguna dependencia. A cambio, lo que no sea una tabla
/// normal (celdas combinadas, fórmulas sin valor calculado) llega tal cual
/// está guardado: combinadas, sólo en la primera celda; fórmulas, con el
/// último valor que calculó Excel.
///
/// Las celdas salen como texto en forma invariante (números con punto,
/// fechas ISO) para que <see cref="DelimitedTextParser"/> decida los tipos
/// con las mismas reglas que un pegado: "00123" guardado como texto sigue
/// siendo una referencia.
/// </summary>
internal sealed class XlsxReader
{
    private const string MainNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string StrictMainNs = "http://purl.oclc.org/ooxml/spreadsheetml/main";
    private const string RelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private const string StrictRelNs = "http://purl.oclc.org/ooxml/officeDocument/relationships";
    private const string PackageRelNs = "http://schemas.openxmlformats.org/package/2006/relationships";

    /// <summary>
    /// Transitional (lo normal) o Strict (Excel "Hoja de cálculo Open XML
    /// estricta"): mismos elementos, otro espacio de nombres.
    /// </summary>
    private static bool IsMain(
        XmlReader reader) =>
        reader.NamespaceURI is MainNs or StrictMainNs;

    /// <summary>
    /// Tope de lo que puede ocupar una parte ya descomprimida. Un ZIP puede
    /// declarar 20 MB y expandirse a gigas; esto lo corta antes.
    /// </summary>
    private const long MaxPartBytes = 256L * 1024 * 1024;

    private readonly ZipArchive _zip;
    private readonly List<string> _sharedStrings;
    private readonly List<CellFormat> _cellFormats;
    private readonly bool _date1904;

    public IReadOnlyList<XlsxSheet> Sheets { get; }

    private XlsxReader(
        ZipArchive zip)
    {
        _zip = zip;

        var workbook =
            Entry("xl/workbook.xml")
            ?? throw new TabularFileException(
                "El fichero no parece un Excel (.xlsx): le falta el libro.");

        var relationships = ReadRelationships("xl/_rels/workbook.xml.rels");

        (Sheets, _date1904) = ReadWorkbook(workbook, relationships);

        _sharedStrings =
            ReadSharedStrings(
                relationships.Values.FirstOrDefault(x => x.Type.EndsWith("/sharedStrings", StringComparison.Ordinal))?.Target
                ?? "xl/sharedStrings.xml");

        _cellFormats =
            ReadStyles(
                relationships.Values.FirstOrDefault(x => x.Type.EndsWith("/styles", StringComparison.Ordinal))?.Target
                ?? "xl/styles.xml");
    }

    public static XlsxReader Open(
        ZipArchive zip) =>
        new(zip);

    /// <summary>
    /// Filas de la hoja con su número (el que se ve en Excel). Las filas sin
    /// ningún valor no aparecen.
    /// </summary>
    public List<XlsxRow> ReadRows(
        XlsxSheet sheet,
        int maxRows,
        int maxCells)
    {
        var entry =
            Entry(sheet.Path)
            ?? throw new TabularFileException(
                $"No se encuentra la hoja '{sheet.Name}' dentro del fichero.");

        var rows = new List<XlsxRow>();
        var cells = 0;

        using var reader = CreateReader(entry);

        XlsxRow? current = null;
        var nextRowNumber = 1;

        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element ||
                !IsMain(reader))
            {
                continue;
            }

            if (reader.LocalName == "row")
            {
                var number =
                    int.TryParse(reader.GetAttribute("r"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var r)
                        ? r
                        : nextRowNumber;

                nextRowNumber = number + 1;

                current = new XlsxRow(number, []);
                rows.Add(current);

                if (rows.Count > maxRows + 1)
                {
                    throw new TabularFileException(
                        $"La hoja '{sheet.Name}' pasa de {maxRows:N0} filas.");
                }

                continue;
            }

            if (reader.LocalName != "c" || current is null)
                continue;

            var column =
                ColumnIndex(reader.GetAttribute("r"))
                ?? current.Cells.Count;

            var value = ReadCell(reader);

            if (string.IsNullOrEmpty(value))
                continue;

            while (current.Cells.Count < column)
                current.Cells.Add(string.Empty);

            if (current.Cells.Count == column)
                current.Cells.Add(value);
            else
                current.Cells[column] = value;

            if (++cells > maxCells)
            {
                throw new TabularFileException(
                    $"La hoja '{sheet.Name}' tiene demasiadas celdas con datos (más de {maxCells:N0}).");
            }
        }

        rows.RemoveAll(x => x.Cells.All(string.IsNullOrWhiteSpace));

        return rows;
    }

    // ---------------------------------------------------------------- celdas

    /// <summary>
    /// Lee una celda &lt;c&gt; entera y deja el lector en su cierre.
    /// </summary>
    private string ReadCell(
        XmlReader reader)
    {
        var type = reader.GetAttribute("t");

        var style =
            int.TryParse(reader.GetAttribute("s"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var s)
                ? s
                : 0;

        if (reader.IsEmptyElement)
            return string.Empty;

        string? raw = null;
        string? inline = null;

        var depth = reader.Depth;

        // Ojo con avanzar dos veces: ReadElementContentAsString ya deja el
        // lector en el nodo siguiente, y un Read() de más se saltaría el
        // cierre de la celda y se comería la siguiente.
        reader.Read();

        while (!reader.EOF &&
               !(reader.NodeType == XmlNodeType.EndElement && reader.Depth == depth))
        {
            if (reader.NodeType == XmlNodeType.Element &&
                IsMain(reader))
            {
                if (reader.LocalName == "v")
                {
                    raw = reader.ReadElementContentAsString();
                    continue;
                }

                if (reader.LocalName == "is")
                    inline = ReadRichText(reader);
            }

            reader.Read();
        }

        switch (type)
        {
            case "s":
                return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) &&
                       index >= 0 &&
                       index < _sharedStrings.Count
                    ? _sharedStrings[index]
                    : string.Empty;

            case "inlineStr":
                return inline ?? string.Empty;

            case "str":
                return raw ?? string.Empty;

            case "b":
                return raw == "1" ? "true" : raw == "0" ? "false" : string.Empty;

            // #N/A, #DIV/0!: para el informe es un valor que no hay.
            case "e":
                return string.Empty;

            case "d":
                return DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var iso)
                    ? FormatDate(iso, hasTime: iso.TimeOfDay != TimeSpan.Zero)
                    : raw ?? string.Empty;

            default:
                return Number(raw, style);
        }
    }

    /// <summary>
    /// Excel guarda todos los números igual; lo que distingue una fecha es
    /// el formato de la celda. Los números se reescriben con la forma más
    /// corta que los representa: el XML trae 33.356200000000001.
    /// </summary>
    private string Number(
        string? raw,
        int style)
    {
        if (string.IsNullOrWhiteSpace(raw) ||
            !double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            return raw ?? string.Empty;
        }

        var format =
            style >= 0 && style < _cellFormats.Count
                ? _cellFormats[style]
                : CellFormat.General;

        if (format.Kind != CellFormatKind.Number &&
            TryFromSerial(number, out var date))
        {
            return format.Kind switch
            {
                CellFormatKind.Time => date.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
                CellFormatKind.DateTime => FormatDate(date, hasTime: true),
                _ => FormatDate(date, hasTime: date.TimeOfDay != TimeSpan.Zero)
            };
        }

        // decimal evita la notación científica (1E-05) que el análisis no
        // reconocería como número.
        if (Math.Abs(number) < 7.9e27)
            return ((decimal)number).ToString(CultureInfo.InvariantCulture);

        return number.ToString("R", CultureInfo.InvariantCulture);
    }

    private bool TryFromSerial(
        double serial,
        out DateTime date)
    {
        date = default;

        if (serial < 0 || serial > 2958465)
            return false;

        try
        {
            date = DateTime.FromOADate(serial);

            if (_date1904)
                date = date.AddDays(1462);

            // Al segundo más cercano: la fracción del día trae decimales
            // que darían 14:29:59.999.
            date = new DateTime(
                (date.Ticks + TimeSpan.TicksPerSecond / 2) /
                TimeSpan.TicksPerSecond *
                TimeSpan.TicksPerSecond);

            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static string FormatDate(
        DateTime date,
        bool hasTime) =>
        hasTime
            ? date.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
            : date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>
    /// "C12" → 2. Nulo si no hay referencia: entonces va a continuación.
    /// </summary>
    internal static int? ColumnIndex(
        string? reference)
    {
        if (string.IsNullOrEmpty(reference))
            return null;

        var index = 0;
        var letters = 0;

        foreach (var character in reference)
        {
            if (character is >= 'A' and <= 'Z')
                index = index * 26 + (character - 'A' + 1);
            else if (character is >= 'a' and <= 'z')
                index = index * 26 + (character - 'a' + 1);
            else
                break;

            if (++letters > 3)
                return null;
        }

        return letters == 0 ? null : index - 1;
    }

    // ----------------------------------------------------------------- libro

    private static (IReadOnlyList<XlsxSheet> Sheets, bool Date1904) ReadWorkbook(
        ZipArchiveEntry entry,
        IReadOnlyDictionary<string, Relationship> relationships)
    {
        var sheets = new List<XlsxSheet>();
        var date1904 = false;

        using var reader = CreateReader(entry);

        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element ||
                !IsMain(reader))
            {
                continue;
            }

            if (reader.LocalName == "workbookPr")
            {
                var value = reader.GetAttribute("date1904");
                date1904 = value is "1" or "true";

                continue;
            }

            if (reader.LocalName != "sheet")
                continue;

            var name = reader.GetAttribute("name") ?? $"Hoja{sheets.Count + 1}";
            var id =
                reader.GetAttribute("id", RelNs)
                ?? reader.GetAttribute("id", StrictRelNs);
            var state = reader.GetAttribute("state");

            if (id is null ||
                !relationships.TryGetValue(id, out var relationship) ||
                !relationship.Type.EndsWith("/worksheet", StringComparison.Ordinal))
            {
                // Hojas de gráfico o de macros: no tienen celdas.
                continue;
            }

            sheets.Add(
                new XlsxSheet(
                    name,
                    relationship.Target,
                    Hidden: state is "hidden" or "veryHidden"));
        }

        if (sheets.Count == 0)
            throw new TabularFileException("El Excel no tiene ninguna hoja con datos.");

        return (sheets, date1904);
    }

    private sealed record Relationship(
        string Type,
        string Target);

    private Dictionary<string, Relationship> ReadRelationships(
        string path)
    {
        var result = new Dictionary<string, Relationship>(StringComparer.Ordinal);

        var entry = Entry(path);

        if (entry is null)
            return result;

        using var reader = CreateReader(entry);

        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element ||
                reader.LocalName != "Relationship" ||
                reader.NamespaceURI != PackageRelNs)
            {
                continue;
            }

            var id = reader.GetAttribute("Id");
            var type = reader.GetAttribute("Type");
            var target = reader.GetAttribute("Target");

            if (id is null || type is null || target is null)
                continue;

            // Relativo a xl/ salvo que empiece por /, que es desde la raíz.
            var resolved =
                target.StartsWith('/')
                    ? target.TrimStart('/')
                    : "xl/" + target;

            result[id] = new Relationship(type, NormalizePath(resolved));
        }

        return result;
    }

    private static string NormalizePath(
        string path)
    {
        var parts = new List<string>();

        foreach (var part in path.Replace('\\', '/').Split('/'))
        {
            if (part is "" or ".")
                continue;

            if (part == "..")
            {
                if (parts.Count > 0)
                    parts.RemoveAt(parts.Count - 1);

                continue;
            }

            parts.Add(part);
        }

        return string.Join('/', parts);
    }

    private List<string> ReadSharedStrings(
        string path)
    {
        var result = new List<string>();

        var entry = Entry(path);

        if (entry is null)
            return result;

        using var reader = CreateReader(entry);

        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element &&
                reader.LocalName == "si" &&
                IsMain(reader))
            {
                result.Add(ReadRichText(reader));
            }
        }

        return result;
    }

    /// <summary>
    /// Texto de un &lt;si&gt; o &lt;is&gt;: un &lt;t&gt; suelto o varios
    /// tramos con formato. La guía fonética (&lt;rPh&gt;, japonés) no es
    /// parte del texto.
    /// </summary>
    private static string ReadRichText(
        XmlReader reader)
    {
        if (reader.IsEmptyElement)
            return string.Empty;

        var depth = reader.Depth;
        var builder = new StringBuilder();

        // Mismo cuidado que en ReadCell: Skip y ReadElementContentAsString
        // ya avanzan.
        reader.Read();

        while (!reader.EOF &&
               !(reader.NodeType == XmlNodeType.EndElement && reader.Depth == depth))
        {
            if (reader.NodeType == XmlNodeType.Element)
            {
                if (reader.LocalName == "rPh")
                {
                    reader.Skip();
                    continue;
                }

                if (reader.LocalName == "t")
                {
                    builder.Append(reader.ReadElementContentAsString());
                    continue;
                }
            }

            reader.Read();
        }

        return builder.ToString();
    }

    // --------------------------------------------------------------- estilos

    internal enum CellFormatKind
    {
        Number,
        Date,
        DateTime,
        Time
    }

    private sealed record CellFormat(
        CellFormatKind Kind)
    {
        public static readonly CellFormat General = new(CellFormatKind.Number);
    }

    private List<CellFormat> ReadStyles(
        string path)
    {
        var result = new List<CellFormat>();

        var entry = Entry(path);

        if (entry is null)
            return result;

        var custom = new Dictionary<int, string>();

        using var reader = CreateReader(entry);

        var inCellXfs = false;

        while (reader.Read())
        {
            if (!IsMain(reader))
                continue;

            if (reader.NodeType == XmlNodeType.EndElement && reader.LocalName == "cellXfs")
            {
                inCellXfs = false;
                continue;
            }

            if (reader.NodeType != XmlNodeType.Element)
                continue;

            switch (reader.LocalName)
            {
                case "numFmt":
                    if (int.TryParse(reader.GetAttribute("numFmtId"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
                        custom[id] = reader.GetAttribute("formatCode") ?? string.Empty;
                    break;

                case "cellXfs":
                    inCellXfs = !reader.IsEmptyElement;
                    break;

                case "xf" when inCellXfs:
                    var formatId =
                        int.TryParse(reader.GetAttribute("numFmtId"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var f)
                            ? f
                            : 0;

                    result.Add(new CellFormat(Classify(formatId, custom)));
                    break;
            }
        }

        return result;
    }

    private static CellFormatKind Classify(
        int formatId,
        IReadOnlyDictionary<int, string> custom)
    {
        if (custom.TryGetValue(formatId, out var code))
            return ClassifyCode(code);

        return formatId switch
        {
            >= 14 and <= 17 => CellFormatKind.Date,
            22 => CellFormatKind.DateTime,
            >= 18 and <= 21 => CellFormatKind.Time,
            >= 45 and <= 47 => CellFormatKind.Time,

            // Formatos de fecha de las versiones asiáticas.
            >= 27 and <= 36 => CellFormatKind.Date,
            >= 50 and <= 58 => CellFormatKind.Date,

            _ => CellFormatKind.Number
        };
    }

    /// <summary>
    /// Un formato propio es de fecha si usa d o y fuera de textos entre
    /// comillas y de corchetes ([Red], [$-es-ES]). La m sola es ambigua
    /// (mes o minuto) y sólo cuenta como fecha si no hay horas.
    /// </summary>
    internal static CellFormatKind ClassifyCode(
        string code)
    {
        // Sólo la primera sección: la de los positivos.
        var builder = new StringBuilder();
        var inQuotes = false;
        var inBrackets = false;

        for (var i = 0; i < code.Length; i++)
        {
            var character = code[i];

            if (inQuotes)
            {
                if (character == '"')
                    inQuotes = false;

                continue;
            }

            if (inBrackets)
            {
                if (character == ']')
                    inBrackets = false;

                continue;
            }

            switch (character)
            {
                case '"':
                    inQuotes = true;
                    continue;

                case '[':
                    // [h]:mm es una duración (horas que pasan de 24); [Red]
                    // o [$-es-ES] no dicen nada del tipo.
                    var close = code.IndexOf(']', i + 1);

                    if (close > i + 1 &&
                        code.AsSpan(i + 1, close - i - 1).ToString().All(c => c is 'h' or 'H' or 'm' or 'M' or 's' or 'S'))
                    {
                        builder.Append('h');
                    }

                    inBrackets = true;
                    continue;

                case '\\':
                case '_':
                case '*':
                    i++;
                    continue;

                case ';':
                    i = code.Length;
                    continue;
            }

            builder.Append(char.ToLowerInvariant(character));
        }

        var cleaned = builder.ToString();

        if (cleaned.Contains("general", StringComparison.Ordinal))
            return CellFormatKind.Number;

        // Sin la e: 0.00E+00 es científico, no un año.
        var hasDate = cleaned.IndexOfAny(['d', 'y']) >= 0;
        var hasTime = cleaned.IndexOfAny(['h', 's']) >= 0;
        var hasMonthOrMinute = cleaned.Contains('m');

        if (hasDate || (hasMonthOrMinute && !hasTime))
            return hasTime ? CellFormatKind.DateTime : CellFormatKind.Date;

        return hasTime
            ? CellFormatKind.Time
            : CellFormatKind.Number;
    }

    // ------------------------------------------------------------------- zip

    private ZipArchiveEntry? Entry(
        string path)
    {
        var entry =
            _zip.GetEntry(path)
            ?? _zip.Entries.FirstOrDefault(x => string.Equals(x.FullName, path, StringComparison.OrdinalIgnoreCase));

        if (entry is not null && entry.Length > MaxPartBytes)
        {
            throw new TabularFileException(
                "El Excel es demasiado grande una vez descomprimido.");
        }

        return entry;
    }

    /// <summary>
    /// Sin DTD ni resolución externa: un XML de un fichero subido no puede
    /// pedir que se lea nada del servidor.
    /// </summary>
    private static XmlReader CreateReader(
        ZipArchiveEntry entry) =>
        XmlReader.Create(
            entry.Open(),
            new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                IgnoreComments = true,
                IgnoreWhitespace = false,
                CloseInput = true
            });
}

internal sealed record XlsxSheet(
    string Name,
    string Path,
    bool Hidden);

internal sealed record XlsxRow(
    int Number,
    List<string> Cells);
