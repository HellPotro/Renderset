using System.Text;

namespace Renderset.Core.Presets;

/// <summary>
/// Assignments en CSV, para darlos de alta en bloque: se descarga una
/// plantilla, se rellena en Excel con los clientes (o lo que sea el
/// contexto) y se vuelve a subir.
///
///     tipo_contexto;valor;preset
///     cliente.codigo;000762;albaran-talleres-pelaez
///     cliente.codigo;000815;albaran-talleres-pelaez
///
/// Punto y coma porque es lo que abre Excel en español sin preguntar; al
/// leer se acepta también coma y tabulador. Los valores se leen como
/// texto: "000762" sigue siendo "000762" (el lector tabular general lo
/// convertiría en el número 762).
/// </summary>
public static class ReportPresetAssignmentCsv
{
    public const string ContextTypeHeader = "tipo_contexto";
    public const string ContextKeyHeader = "valor";
    public const string PresetHeader = "preset";

    public sealed record Row(
        int Line,
        string ContextType,
        string ContextKey,
        string? PresetId);

    public sealed record ParseResult(
        IReadOnlyList<Row> Rows,
        IReadOnlyList<string> Errors);

    /// <summary>
    /// Plantilla: cabecera, las asignaciones que ya existen y unas filas de
    /// ejemplo con el tipo de contexto ya puesto, para no tener que saber
    /// cómo se escribe.
    /// </summary>
    public static string Build(
        string contextType,
        string presetId,
        IEnumerable<ReportPresetAssignment> existing,
        int emptyRows = 10)
    {
        var builder = new StringBuilder();

        builder.Append(ContextTypeHeader).Append(';')
            .Append(ContextKeyHeader).Append(';')
            .AppendLine(PresetHeader);

        foreach (var assignment in existing
                     .Where(x => !string.IsNullOrWhiteSpace(x.ContextType))
                     .OrderBy(x => x.ContextType)
                     .ThenBy(x => x.ContextKey))
        {
            builder.Append(Escape(assignment.ContextType!)).Append(';')
                .Append(Escape(assignment.ContextKey ?? string.Empty)).Append(';')
                .AppendLine(Escape(assignment.PresetId));
        }

        for (var i = 0; i < emptyRows; i++)
        {
            builder.Append(Escape(contextType)).Append(';')
                .Append(';')
                .AppendLine(Escape(presetId));
        }

        return builder.ToString();
    }

    /// <summary>
    /// Lee el CSV. Las filas sin valor (las de ejemplo que no se rellenaron)
    /// se ignoran; las que no tienen tipo de contexto son error.
    /// </summary>
    public static ParseResult Parse(
        string? text)
    {
        var rows = new List<Row>();
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(text))
        {
            errors.Add("El fichero está vacío.");
            return new ParseResult(rows, errors);
        }

        var lines =
            text.TrimStart('﻿')
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Split('\n');

        var headerIndex =
            Array.FindIndex(lines, x => !string.IsNullOrWhiteSpace(x));

        var separator = DetectSeparator(lines[headerIndex]);

        var header =
            Split(lines[headerIndex], separator)
                .Select(x => x.Trim().ToLowerInvariant())
                .ToList();

        var typeColumn = IndexOf(header, ContextTypeHeader, "contexttype", "tipo", "context", "contexto");
        var keyColumn = IndexOf(header, ContextKeyHeader, "contextkey", "clave", "key", "value");
        var presetColumn = IndexOf(header, PresetHeader, "presetid");

        // Sin cabecera reconocible se asume el orden de la plantilla.
        var hasHeader = typeColumn >= 0 || keyColumn >= 0;

        if (!hasHeader)
        {
            typeColumn = 0;
            keyColumn = 1;
            presetColumn = 2;
        }
        else if (typeColumn < 0 || keyColumn < 0)
        {
            errors.Add($"Faltan columnas: la cabecera tiene que llevar '{ContextTypeHeader}' y '{ContextKeyHeader}'.");
            return new ParseResult(rows, errors);
        }

        for (var i = hasHeader ? headerIndex + 1 : headerIndex; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
                continue;

            var cells = Split(lines[i], separator);

            string Cell(int index) =>
                index >= 0 && index < cells.Count
                    ? cells[index].Trim()
                    : string.Empty;

            var type = Cell(typeColumn);
            var key = Cell(keyColumn);
            var preset = Cell(presetColumn);

            if (string.IsNullOrWhiteSpace(key))
                continue;

            if (string.IsNullOrWhiteSpace(type))
            {
                errors.Add($"Línea {i + 1}: falta el tipo de contexto para '{key}'.");
                continue;
            }

            rows.Add(
                new Row(
                    i + 1,
                    type,
                    key,
                    string.IsNullOrWhiteSpace(preset) ? null : preset));
        }

        var duplicated =
            rows
                .GroupBy(x => (x.ContextType.ToLowerInvariant(), x.ContextKey.ToLowerInvariant()))
                .Where(x => x.Count() > 1)
                .Select(x => $"{x.First().ContextType} = {x.First().ContextKey}")
                .ToList();

        foreach (var item in duplicated)
            errors.Add($"Repetido: {item}. Cada contexto sólo puede ir a un preset.");

        return new ParseResult(rows, errors);
    }

    private static int IndexOf(
        List<string> header,
        params string[] names) =>
        header.FindIndex(x => names.Contains(x, StringComparer.OrdinalIgnoreCase));

    private static char DetectSeparator(
        string line)
    {
        var candidates = new[] { ';', ',', '\t' };

        return candidates
            .OrderByDescending(c => line.Count(x => x == c))
            .First();
    }

    /// <summary>
    /// Separa una línea respetando comillas ("a;b" es un solo valor).
    /// </summary>
    private static List<string> Split(
        string line,
        char separator)
    {
        var cells = new List<string>();
        var current = new StringBuilder();
        var quoted = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];

            if (quoted)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    quoted = false;
                }
                else
                {
                    current.Append(c);
                }

                continue;
            }

            if (c == '"')
            {
                quoted = true;
                continue;
            }

            if (c == separator)
            {
                cells.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(c);
        }

        cells.Add(current.ToString());

        return cells;
    }

    private static string Escape(
        string value) =>
        value.IndexOfAny([';', ',', '"', '\n', '\r']) >= 0
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
}
