using System.IO.Compression;
using System.Text;

namespace Renderset.Core.DataSources;

/// <summary>
/// Lee un fichero subido (Excel o texto delimitado) y lo deja en el mismo
/// <see cref="TabularPayload"/> que un pegado de SSMS. A partir de ahí el
/// mapping, el asistente y la generación no saben de dónde vinieron las
/// filas.
///
/// El formato se decide por el contenido y no sólo por la extensión: un
/// "informe.xls" que en realidad es un CSV (muchos ERP lo hacen) se lee como
/// CSV, y un .xls de verdad se rechaza con un mensaje que dice qué hacer.
/// </summary>
public static class TabularFile
{
    public const long MaxBytes = 20L * 1024 * 1024;

    /// <summary>
    /// Igual que el límite de filas de <c>POST /api/render</c>.
    /// </summary>
    public const int MaxRows = 50_000;

    public const int MaxCells = 2_000_000;

    /// <summary>
    /// Para el accept del input: el navegador sólo lo usa para filtrar el
    /// diálogo, la comprobación de verdad es la del contenido.
    /// </summary>
    public const string Accept =
        ".xlsx,.xlsm,.csv,.tsv,.txt,.xls," +
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet," +
        "text/csv,text/tab-separated-values,text/plain";

    /// <param name="content">El fichero entero. Se lee en memoria: hay que poder volver a abrirlo para cambiar de hoja.</param>
    /// <exception cref="TabularFileException">Fichero no soportado, dañado o demasiado grande. El mensaje es para el usuario.</exception>
    public static TabularFileResult Read(
        ReadOnlyMemory<byte> content,
        string fileName,
        TabularFileOptions? options = null)
    {
        options ??= new TabularFileOptions();

        if (content.Length == 0)
            throw new TabularFileException("El fichero está vacío.");

        if (content.Length > MaxBytes)
            throw new TabularFileException($"El fichero pasa de {MaxBytes / 1024 / 1024} MB.");

        var span = content.Span;

        // PK\x03\x04: un ZIP. Todo Excel moderno lo es.
        if (span.Length >= 4 &&
            span[0] == 0x50 && span[1] == 0x4B && span[2] == 0x03 && span[3] == 0x04)
        {
            return ReadXlsx(content, fileName, options);
        }

        // D0 CF 11 E0: contenedor OLE. Un .xls de Excel 97-2003, o un .xlsx
        // con contraseña, que Excel guarda cifrado dentro de un OLE.
        if (span.Length >= 4 &&
            span[0] == 0xD0 && span[1] == 0xCF && span[2] == 0x11 && span[3] == 0xE0)
        {
            throw new TabularFileException(
                "Es un Excel antiguo (.xls) o un Excel con contraseña. " +
                "Ábrelo en Excel y guárdalo como .xlsx sin contraseña, o como CSV.");
        }

        return ReadText(content, fileName, options);
    }

    // ------------------------------------------------------------ texto

    private static TabularFileResult ReadText(
        ReadOnlyMemory<byte> content,
        string fileName,
        TabularFileOptions options)
    {
        var (text, encoding) = Decode(content.Span);

        if (text.IndexOf('\0') >= 0)
        {
            throw new TabularFileException(
                "El fichero no es texto ni un Excel (.xlsx). Sube un .xlsx, .csv o .txt.");
        }

        IReadOnlyList<string> renamed;

        var payload =
            options.InferTypes
                ? DelimitedTextParser.Parse(text, out renamed)
                : DelimitedTextParser.ParseAsText(text, out renamed);

        return new TabularFileResult
        {
            FileName = fileName,
            Format = TabularFileFormat.DelimitedText,
            Payload = payload,
            RenamedHeaders = renamed,
            Encoding = encoding
        };
    }

    /// <summary>
    /// Un CSV guardado desde Excel en español no es UTF-8 sino Windows-1252
    /// ("Albarán" llega como "Albar�n" si se lee mal). Orden: marca de
    /// orden de bytes si la hay; si no, UTF-8 si es válido; si no, 1252.
    /// </summary>
    internal static (string Text, string Encoding) Decode(
        ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return (Encoding.UTF8.GetString(bytes[3..]), "UTF-8");

        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            return (Encoding.Unicode.GetString(bytes[2..]), "UTF-16");

        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            return (Encoding.BigEndianUnicode.GetString(bytes[2..]), "UTF-16");

        try
        {
            var strict = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

            return (strict.GetString(bytes), "UTF-8");
        }
        catch (DecoderFallbackException)
        {
            return (Windows1252.GetString(bytes), "Windows-1252");
        }
    }

    private static Encoding Windows1252
    {
        get
        {
            // Las páginas de código vienen con .NET pero hay que darlas de
            // alta; registrar dos veces no pasa nada.
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            return Encoding.GetEncoding(1252);
        }
    }

    // ------------------------------------------------------------ excel

    private static TabularFileResult ReadXlsx(
        ReadOnlyMemory<byte> content,
        string fileName,
        TabularFileOptions options)
    {
        ZipArchive zip;

        try
        {
            zip = new ZipArchive(new MemoryStream(content.ToArray(), writable: false), ZipArchiveMode.Read);
        }
        catch (InvalidDataException)
        {
            throw new TabularFileException("El Excel está dañado o no es un .xlsx.");
        }

        using (zip)
        {
            if (zip.GetEntry("xl/workbook.xml") is null &&
                zip.Entries.All(x => !string.Equals(x.FullName, "xl/workbook.xml", StringComparison.OrdinalIgnoreCase)))
            {
                // .docx, .ods, un .zip cualquiera...
                throw new TabularFileException(
                    "El fichero es un ZIP pero no un Excel (.xlsx). Si es de LibreOffice (.ods), guárdalo como .xlsx o CSV.");
            }

            XlsxReader reader;

            try
            {
                reader = XlsxReader.Open(zip);
            }
            catch (System.Xml.XmlException)
            {
                throw new TabularFileException("El Excel está dañado: no se puede leer el libro.");
            }

            var sheet =
                reader.Sheets.FirstOrDefault(x => string.Equals(x.Name, options.Sheet, StringComparison.OrdinalIgnoreCase))
                ?? reader.Sheets.FirstOrDefault(x => !x.Hidden)
                ?? reader.Sheets[0];

            List<XlsxRow> rows;

            try
            {
                rows = reader.ReadRows(sheet, MaxRows, MaxCells);
            }
            catch (System.Xml.XmlException)
            {
                throw new TabularFileException($"La hoja '{sheet.Name}' está dañada.");
            }
            catch (InvalidDataException)
            {
                throw new TabularFileException("El Excel está dañado o no es un .xlsx.");
            }

            var sheets =
                reader.Sheets
                    .Where(x => !x.Hidden || x == sheet)
                    .Select(x => x.Name)
                    .ToList();

            if (rows.Count == 0)
            {
                return new TabularFileResult
                {
                    FileName = fileName,
                    Format = TabularFileFormat.Excel,
                    Payload = new TabularPayload(),
                    Sheets = sheets,
                    Sheet = sheet.Name
                };
            }

            var headerIndex = HeaderIndex(rows, options.HeaderRow);

            var grid =
                rows
                    .Skip(headerIndex)
                    .Select(x => x.Cells)
                    .ToList();

            var renamed = new List<string>();

            var payload =
                DelimitedTextParser.FromGrid(
                    grid,
                    renamed,
                    firstRowIsHeader: true,
                    ambiguousDecimalSeparator: '.',
                    inferTypes: options.InferTypes);

            return new TabularFileResult
            {
                FileName = fileName,
                Format = TabularFileFormat.Excel,
                Payload = payload,
                RenamedHeaders = renamed,
                Sheets = sheets,
                Sheet = sheet.Name,
                HeaderRow = rows[headerIndex].Number,
                HeaderRowDetected = options.HeaderRow is null,
                SkippedRowsAbove = headerIndex
            };
        }
    }

    /// <summary>
    /// Muchos Excel llevan encima de la tabla un título, la fecha o el
    /// filtro con el que se sacó ("Salidas de octubre"). La cabecera es la
    /// primera fila que ocupa al menos la mitad del ancho de la tabla; con
    /// <see cref="TabularFileOptions.HeaderRow"/> se fija a mano.
    /// </summary>
    internal static int HeaderIndex(
        IReadOnlyList<XlsxRow> rows,
        int? headerRow)
    {
        if (headerRow is { } wanted)
        {
            // La primera fila con datos en esa posición o después.
            for (var i = 0; i < rows.Count; i++)
            {
                if (rows[i].Number >= wanted)
                    return i;
            }

            throw new TabularFileException(
                $"No hay datos a partir de la fila {wanted}.");
        }

        static int Filled(XlsxRow row) =>
            row.Cells.Count(x => !string.IsNullOrWhiteSpace(x));

        // Basta con mirar el principio: los títulos van arriba.
        var sample = rows.Take(50).ToList();

        var widest = sample.Max(Filled);

        var threshold = Math.Max(2, (widest + 1) / 2);

        for (var i = 0; i < sample.Count; i++)
        {
            if (Filled(sample[i]) >= threshold)
                return i;
        }

        return 0;
    }
}

public sealed class TabularFileOptions
{
    /// <summary>
    /// Hoja de un Excel. Nula = la primera visible.
    /// </summary>
    public string? Sheet { get; set; }

    /// <summary>
    /// Número de fila de la cabecera, tal como se ve en Excel. Nula =
    /// detectarla (se saltan los títulos de encima).
    /// </summary>
    public int? HeaderRow { get; set; }

    /// <summary>
    /// Falso: todo como texto, sin adivinar números ni fechas.
    /// </summary>
    public bool InferTypes { get; set; } = true;
}

public enum TabularFileFormat
{
    DelimitedText,
    Excel
}

public sealed class TabularFileResult
{
    public required string FileName { get; init; }

    public required TabularFileFormat Format { get; init; }

    public required TabularPayload Payload { get; init; }

    public IReadOnlyList<string> RenamedHeaders { get; init; } = [];

    /// <summary>
    /// Hojas que se pueden elegir. Vacío en un CSV.
    /// </summary>
    public IReadOnlyList<string> Sheets { get; init; } = [];

    public string? Sheet { get; init; }

    /// <summary>
    /// Fila de la cabecera en Excel (1, 2, 3...). Nula en un CSV.
    /// </summary>
    public int? HeaderRow { get; init; }

    public bool HeaderRowDetected { get; init; }

    /// <summary>
    /// Filas con datos que había encima de la cabecera y se han dejado
    /// fuera (títulos, filtros).
    /// </summary>
    public int SkippedRowsAbove { get; init; }

    /// <summary>
    /// Codificación con la que se ha leído un CSV, para avisar si no era
    /// UTF-8.
    /// </summary>
    public string? Encoding { get; init; }
}

/// <summary>
/// Un fichero que no se puede leer. El mensaje está pensado para enseñarlo
/// tal cual: es una validación local, no un error de la API.
/// </summary>
public sealed class TabularFileException(
    string message)
    : Exception(message);
