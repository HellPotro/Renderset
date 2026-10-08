using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Renderset.Core.DataSources;

namespace Renderset.Deca.Import;

/// <summary>
/// DeCA desde una hoja: un CSV o un Excel con una fila por envío.
///
/// Las filas con el mismo valor en la columna "DeCA" son un mismo DeCA (sus
/// envíos). Sin esa columna, o con ella vacía, se juntan las filas que
/// comparten cargador, transportista, fecha, hora, vehículo, autorización,
/// observaciones y referencia: es lo que hace falta para que varios envíos
/// vayan en un DeCA (mismo cargador contractual y transportista efectivo).
///
/// Los datos de documento se toman de la primera fila del grupo; si otra
/// fila dice otra cosa se avisa, no se mezcla.
///
/// Aquí sólo se lee: la validación es la de siempre (DecaValidator), sobre
/// cada DeCA ya montado.
/// </summary>
public static partial class DecaImport
{
    /// <summary>
    /// Columnas de la plantilla, en orden. El nombre es el de la plantilla;
    /// los alias son otras formas razonables de llamarla.
    /// </summary>
    public static IReadOnlyList<DecaImportColumn> Columns { get; } =
    [
        new(DecaImportField.Group, "DeCA", false, ["grupo", "agrupar", "deca", "numero deca", "documento"]),
        new(DecaImportField.Reference, "Referencia", false, ["referencia propia", "salida", "pedido"]),
        new(DecaImportField.ShipperName, "Cargador razón social", true, ["cargador", "cargador nombre", "razon social cargador", "cargador contractual"]),
        new(DecaImportField.ShipperTaxId, "Cargador NIF", true, ["nif cargador", "cif cargador", "cargador cif"]),
        new(DecaImportField.ShipperAddress, "Cargador domicilio", true, ["domicilio cargador", "cargador direccion", "direccion cargador"]),
        new(DecaImportField.CarrierName, "Transportista razón social", true, ["transportista", "transportista nombre", "razon social transportista", "transportista efectivo"]),
        new(DecaImportField.CarrierTaxId, "Transportista NIF", true, ["nif transportista", "cif transportista", "transportista cif"]),
        new(DecaImportField.TransportDate, "Fecha transporte", true, ["fecha", "fecha del transporte", "fecha de transporte"]),
        new(DecaImportField.StartTime, "Hora inicio", false, ["hora", "hora de inicio", "hora prevista"]),
        new(DecaImportField.TractorPlate, "Matrícula tractora", true, ["matricula", "tractora", "matricula tractor", "matricula vehiculo", "vehiculo", "rigido"]),
        new(DecaImportField.TrailerPlate, "Matrícula remolque", false, ["remolque", "semirremolque", "matricula semirremolque"]),
        new(DecaImportField.SpecialAuthorization, "Autorización especial", false, ["autorizacion", "autorizacion especial de circulacion"]),
        new(DecaImportField.Observations, "Observaciones", false, ["reservas", "observaciones o reservas"]),
        new(DecaImportField.Origin, "Origen", true, ["lugar de origen", "carga"]),
        new(DecaImportField.Destination, "Destino", true, ["lugar de destino", "descarga"]),
        new(DecaImportField.Goods, "Mercancía", true, ["naturaleza", "naturaleza de la mercancia", "producto"]),
        new(DecaImportField.WeightKg, "Peso kg", false, ["peso", "peso (kg)", "kg", "peso neto"]),
        new(DecaImportField.AlternativeQuantity, "Cantidad", false, ["cantidad alternativa", "magnitud"]),
        new(DecaImportField.AlternativeUnit, "Unidad", false, ["unidad alternativa", "unidades"]),
        new(DecaImportField.Notes, "Notas del envío", false, ["notas", "notas envio", "observaciones del envio"])
    ];

    /// <summary>
    /// Separador de la plantilla: el que espera Excel en español al abrir
    /// un CSV con doble clic.
    /// </summary>
    public const char TemplateSeparator = ';';

    public const string TemplateFileName = "plantilla-deca.csv";

    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-ES");

    private static readonly string[] DateFormats =
    [
        "dd/MM/yyyy", "d/M/yyyy", "dd/MM/yy", "d/M/yy",
        "dd-MM-yyyy", "d-M-yyyy", "dd.MM.yyyy",
        "yyyy-MM-dd", "yyyy/MM/dd",
        "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd HH:mm"
    ];

    private static readonly string[] TimeFormats =
    [
        "HH:mm", "H:mm", "HH:mm:ss", "H:mm:ss", "HH.mm", "HHmm"
    ];

    // ------------------------------------------------------------ plantilla

    /// <summary>
    /// CSV con la cabecera y dos DeCA de ejemplo (el primero con dos envíos),
    /// separado por punto y coma. Sin la marca UTF-8: la pone quien lo
    /// descarga, para que Excel lea bien las tildes.
    /// </summary>
    public static string Template(
        DateOnly transportDate)
    {
        var date = transportDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

        string[][] rows =
        [
            ["1", "Salida 3129", "Talleres Ejemplo, S.L.", "B12345674", "Pol. Ind. Campollano, Calle C 12, 02007 Albacete",
             "Transportes del Levante, S.A.", "A87654323", date, "07:30", "1234BCD", "R5678BCD", "",
             "Descarga en muelle 4", "Albacete (02007)", "Almussafes, Valencia (46440)", "Piezas de estampación", "12500", "", "", ""],

            ["1", "Salida 3129", "Talleres Ejemplo, S.L.", "B12345674", "Pol. Ind. Campollano, Calle C 12, 02007 Albacete",
             "Transportes del Levante, S.A.", "A87654323", date, "07:30", "1234BCD", "R5678BCD", "",
             "Descarga en muelle 4", "Albacete (02007)", "Martorell, Barcelona (08760)", "Conjuntos soldados", "4800", "22", "jaulas", "Entrega antes de las 14:00"],

            ["2", "Salida 3130", "Talleres Ejemplo, S.L.", "B12345674", "Pol. Ind. Campollano, Calle C 12, 02007 Albacete",
             "Transportes del Levante, S.A.", "A87654323", date, "", "5678CDF", "", "",
             "", "Albacete (02007)", "Getafe, Madrid (28906)", "Bastidores", "6300", "", "", ""]
        ];

        var builder = new StringBuilder();

        builder.AppendLine(string.Join(TemplateSeparator, Columns.Select(x => Quote(x.Header))));

        foreach (var row in rows)
            builder.AppendLine(string.Join(TemplateSeparator, row.Select(Quote)));

        return builder.ToString();
    }

    private static string Quote(
        string value) =>
        value.IndexOfAny([TemplateSeparator, '"', '\n', '\r']) >= 0
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;

    // ------------------------------------------------------------ lectura

    /// <param name="payload">Filas leídas con <see cref="TabularFileOptions.InferTypes"/> a falso (todo texto).</param>
    /// <param name="decimalPoint">
    /// Verdadero para un Excel (los números llegan con punto); falso para un
    /// CSV, donde se leen como en España (12.500 son doce mil quinientos).
    /// </param>
    /// <param name="firstDataRow">Número de la primera fila de datos en la hoja, para los avisos.</param>
    public static DecaImportResult Read(
        TabularPayload payload,
        bool decimalPoint,
        int firstDataRow = 2)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var result = new DecaImportResult();

        // Columna del fichero → campo.
        var map = new Dictionary<DecaImportField, int>();

        for (var i = 0; i < payload.Columns.Count; i++)
        {
            var column = Match(payload.Columns[i].Name);

            if (column is null)
            {
                result.IgnoredColumns.Add(payload.Columns[i].Name);
                continue;
            }

            // Repetida: vale la primera.
            map.TryAdd(column.Field, i);
        }

        foreach (var required in Columns.Where(x => x.Required && !map.ContainsKey(x.Field)))
            result.Errors.Add($"Falta la columna «{required.Header}».");

        if (!map.ContainsKey(DecaImportField.WeightKg) && !map.ContainsKey(DecaImportField.AlternativeQuantity))
            result.Errors.Add("Falta la columna «Peso kg» (o «Cantidad» y «Unidad» si el peso no procede).");

        var groups = new Dictionary<string, DecaImportItem>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < payload.Rows.Count; index++)
        {
            var values = payload.Rows[index];
            var rowNumber = firstDataRow + index;

            string? Get(DecaImportField field) =>
                map.TryGetValue(field, out var ordinal) && ordinal < values.Count
                    ? Clean(values[ordinal])
                    : null;

            if (Columns.All(x => Get(x.Field) is null))
                continue;

            var document = ReadDocument(Get, rowNumber, out var documentProblems);

            var group = Get(DecaImportField.Group);

            var key =
                group is not null
                    ? "g:" + group
                    : "s:" + Signature(document);

            if (!groups.TryGetValue(key, out var item))
            {
                item = new DecaImportItem
                {
                    Key = group ?? $"Fila {rowNumber}",
                    Data = document
                };

                groups.Add(key, item);
                result.Items.Add(item);
            }
            else
            {
                Compare(item, document, rowNumber);
            }

            item.Rows.Add(rowNumber);
            item.Problems.AddRange(documentProblems);

            var shipment = ReadShipment(Get, rowNumber, decimalPoint, item.Problems);

            if (!DecaNormalizer.IsBlank(shipment))
                item.Data.Shipments.Add(shipment);
        }

        if (result.Items.Count == 0 && result.Errors.Count == 0)
            result.Errors.Add("No hay ninguna fila con datos.");

        return result;
    }

    private static DecaData ReadDocument(
        Func<DecaImportField, string?> get,
        int row,
        out List<string> problems)
    {
        problems = [];

        var data =
            new DecaData
            {
                Reference = get(DecaImportField.Reference),
                Shipper = new DecaParty
                {
                    Name = get(DecaImportField.ShipperName),
                    TaxId = get(DecaImportField.ShipperTaxId),
                    Address = get(DecaImportField.ShipperAddress)
                },
                Carrier = new DecaParty
                {
                    Name = get(DecaImportField.CarrierName),
                    TaxId = get(DecaImportField.CarrierTaxId)
                },
                Vehicle = new DecaVehicle
                {
                    TractorPlate = get(DecaImportField.TractorPlate),
                    TrailerPlate = get(DecaImportField.TrailerPlate)
                },
                SpecialAuthorization = get(DecaImportField.SpecialAuthorization),
                Observations = get(DecaImportField.Observations)
            };

        var date = get(DecaImportField.TransportDate);

        if (date is not null)
        {
            if (TryDate(date, out var parsed))
                data.TransportDate = parsed;
            else
                problems.Add($"Fila {row}: la fecha «{date}» no se entiende. Escríbela como 09/10/2026.");
        }

        var time = get(DecaImportField.StartTime);

        if (time is not null)
        {
            if (TryTime(time, out var parsed))
                data.StartTime = parsed;
            else
                problems.Add($"Fila {row}: la hora «{time}» no se entiende. Escríbela como 07:30.");
        }

        return data;
    }

    private static DecaShipment ReadShipment(
        Func<DecaImportField, string?> get,
        int row,
        bool decimalPoint,
        List<string> problems)
    {
        var shipment =
            new DecaShipment
            {
                Origin = get(DecaImportField.Origin),
                Destination = get(DecaImportField.Destination),
                Goods = get(DecaImportField.Goods),
                AlternativeUnit = get(DecaImportField.AlternativeUnit),
                Notes = get(DecaImportField.Notes)
            };

        var weight = get(DecaImportField.WeightKg);

        if (weight is not null)
        {
            if (TryNumber(weight, decimalPoint, out var parsed))
                shipment.WeightKg = parsed;
            else
                problems.Add($"Fila {row}: el peso «{weight}» no es un número.");
        }

        var quantity = get(DecaImportField.AlternativeQuantity);

        if (quantity is not null)
        {
            if (TryNumber(quantity, decimalPoint, out var parsed))
                shipment.AlternativeQuantity = parsed;
            else
                problems.Add($"Fila {row}: la cantidad «{quantity}» no es un número.");
        }

        return shipment;
    }

    /// <summary>
    /// Lo que tiene que ser igual para ir en el mismo DeCA.
    /// </summary>
    private static string Signature(
        DecaData data) =>
        string.Join(
            "\u001f",
            Key(data.Shipper.TaxId),
            Key(data.Shipper.Name),
            Key(data.Carrier.TaxId),
            Key(data.Carrier.Name),
            data.TransportDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            data.StartTime?.ToString("HH:mm", CultureInfo.InvariantCulture),
            Key(data.Vehicle.TractorPlate),
            Key(data.Vehicle.TrailerPlate),
            Key(data.SpecialAuthorization),
            Key(data.Observations),
            Key(data.Reference));

    private static string Key(
        string? value) =>
        new string(
            (value ?? string.Empty)
                .Where(char.IsLetterOrDigit)
                .Select(char.ToUpperInvariant)
                .ToArray());

    /// <summary>
    /// Una fila del mismo DeCA con otros datos de documento: se avisa y se
    /// queda lo de la primera.
    /// </summary>
    private static void Compare(
        DecaImportItem item,
        DecaData other,
        int row)
    {
        var first = item.Data;
        var firstRow = item.Rows.Count > 0 ? item.Rows[0] : row;

        void Check(string name, string? a, string? b)
        {
            if (b is not null && !string.Equals(Key(a), Key(b), StringComparison.Ordinal))
                item.Problems.Add($"Fila {row}: «{name}» no coincide con la fila {firstRow} del mismo DeCA. Se usa el de la fila {firstRow}.");
        }

        Check("Cargador NIF", first.Shipper.TaxId, other.Shipper.TaxId);
        Check("Cargador razón social", first.Shipper.Name, other.Shipper.Name);
        Check("Transportista NIF", first.Carrier.TaxId, other.Carrier.TaxId);
        Check("Transportista razón social", first.Carrier.Name, other.Carrier.Name);
        Check("Matrícula tractora", first.Vehicle.TractorPlate, other.Vehicle.TractorPlate);
        Check("Matrícula remolque", first.Vehicle.TrailerPlate, other.Vehicle.TrailerPlate);

        if (other.TransportDate is not null && other.TransportDate != first.TransportDate)
            item.Problems.Add($"Fila {row}: «Fecha transporte» no coincide con la fila {firstRow} del mismo DeCA. Se usa la de la fila {firstRow}.");
    }

    private static DecaImportColumn? Match(
        string? header)
    {
        var key = Normalize(header);

        if (key.Length == 0)
            return null;

        return Columns.FirstOrDefault(x => Normalize(x.Header) == key)
               ?? Columns.FirstOrDefault(x => x.Aliases.Any(alias => Normalize(alias) == key));
    }

    /// <summary>
    /// Minúsculas, sin tildes y sólo letras y números: "Matrícula
    /// tractora", "MATRICULA_TRACTORA" y "matricula tractora " son la misma.
    /// </summary>
    internal static string Normalize(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsLetterOrDigit(c))
                builder.Append(char.ToLowerInvariant(c));
        }

        return builder.ToString();
    }

    private static string? Clean(
        object? value)
    {
        var text = value switch
        {
            null => null,
            string s => s,
            DateTime d => d.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()
        };

        return string.IsNullOrWhiteSpace(text)
            ? null
            : text.Trim();
    }

    internal static bool TryDate(
        string value,
        out DateOnly date)
    {
        if (DateTime.TryParseExact(value, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var parsed))
        {
            date = DateOnly.FromDateTime(parsed);
            return true;
        }

        date = default;
        return false;
    }

    internal static bool TryTime(
        string value,
        out TimeOnly time)
    {
        // Un Excel con formato de fecha y hora en la celda.
        if (DateTime.TryParseExact(value, ["yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd'T'HH:mm:ss"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var stamp))
        {
            time = TimeOnly.FromDateTime(stamp);
            return true;
        }

        if (DateTime.TryParseExact(value, TimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var parsed))
        {
            time = TimeOnly.FromDateTime(parsed);
            return true;
        }

        time = default;
        return false;
    }

    /// <summary>
    /// En un CSV español la coma es el decimal y el punto, los miles
    /// ("12.500" son 12500; "12,5", doce y medio). Un Excel ya los da con
    /// punto decimal.
    /// </summary>
    internal static bool TryNumber(
        string value,
        bool decimalPoint,
        out decimal number)
    {
        var text = value
            .Replace(" ", string.Empty)
            .Replace(" ", string.Empty)
            .Replace("kg", string.Empty, StringComparison.OrdinalIgnoreCase);

        // Un número de Excel llega sin miles ("12500.5"). "12.500" o "12,5"
        // sólo pueden venir de una celda con formato de texto, escrita a la
        // española: se leen como en un CSV.
        if (decimalPoint &&
            !text.Contains(',') &&
            !SpanishThousands().IsMatch(text))
        {
            return decimal.TryParse(
                text,
                NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out number);
        }

        // "12.5" sin coma no son 125: un punto que no separa tres cifras es
        // el decimal de alguien que lo ha escrito a la inglesa.
        var dot = text.LastIndexOf('.');

        if (dot >= 0 && !text.Contains(',') && text.Length - dot - 1 != 3)
        {
            return decimal.TryParse(
                text,
                NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out number);
        }

        return decimal.TryParse(
            text,
            NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands | NumberStyles.AllowLeadingSign,
            Spanish,
            out number);
    }

    [GeneratedRegex(@"^-?\d{1,3}(\.\d{3})+$")]
    private static partial Regex SpanishThousands();
}

public enum DecaImportField
{
    Group,
    Reference,
    ShipperName,
    ShipperTaxId,
    ShipperAddress,
    CarrierName,
    CarrierTaxId,
    TransportDate,
    StartTime,
    TractorPlate,
    TrailerPlate,
    SpecialAuthorization,
    Observations,
    Origin,
    Destination,
    Goods,
    WeightKg,
    AlternativeQuantity,
    AlternativeUnit,
    Notes
}

/// <param name="Required">Si el fichero tiene que traer la columna (aunque alguna celda vaya vacía).</param>
public sealed record DecaImportColumn(
    DecaImportField Field,
    string Header,
    bool Required,
    string[] Aliases);

public sealed class DecaImportResult
{
    public List<DecaImportItem> Items { get; } = [];

    /// <summary>
    /// Problemas del fichero entero (faltan columnas): con alguno, no se
    /// emite nada.
    /// </summary>
    public List<string> Errors { get; } = [];

    /// <summary>
    /// Columnas que no son de la plantilla. No impiden nada.
    /// </summary>
    public List<string> IgnoredColumns { get; } = [];
}

/// <summary>
/// Un DeCA de la hoja: sus filas, sus datos y lo que no se ha podido leer.
/// </summary>
public sealed class DecaImportItem
{
    /// <summary>
    /// Valor de la columna DeCA o, sin ella, "Fila N".
    /// </summary>
    public required string Key { get; init; }

    public required DecaData Data { get; init; }

    public List<int> Rows { get; } = [];

    /// <summary>
    /// Valores que no se entienden (fecha, número) y filas que no cuadran
    /// con su DeCA. Son de lectura; los de la norma los da DecaValidator.
    /// </summary>
    public List<string> Problems { get; } = [];
}
