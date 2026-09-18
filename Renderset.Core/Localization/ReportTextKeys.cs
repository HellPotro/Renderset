namespace Renderset.Core.Localization;

/// <summary>
/// Convención de claves del diccionario. Las claves se derivan de la
/// definición al inferir el esquema y a partir de ahí son estables: si el
/// origen renombra un campo, la clave no se recalcula.
/// </summary>
public static class ReportTextKeys
{
    public const string HeaderTitle = "header.title";

    public const string HeaderSubtitle = "header.subtitle";

    public const string FooterText = "footer.text";

    public static string Field(
        string fieldId)
    {
        return $"field.{fieldId}";
    }

    public static string Column(
        string tableId,
        string columnId)
    {
        return $"column.{tableId}.{columnId}";
    }

    public static string Section(
        string sectionId)
    {
        return $"section.{sectionId}";
    }

    public static string BlockTitle(
        string blockId)
    {
        return $"block.{blockId}.title";
    }

    public static string BlockSubtitle(
        string blockId)
    {
        return $"block.{blockId}.subtitle";
    }

    public static string BlockText(
        string blockId)
    {
        return $"block.{blockId}.text";
    }

    /// <summary>
    /// Clave del valor de una variable traducible. Vive en el ámbito global
    /// del tenant, igual que la propia variable: no pertenece a ningún
    /// report.
    /// </summary>
    public static string Variable(
        string key)
    {
        return $"variable.{key}";
    }

    /// <summary>
    /// Clave de una línea de datos de cabecera. Cuelga del id de la línea y
    /// no del bloque: así el texto no se pierde mientras se crea el bloque,
    /// cuando todavía no hay identificador, y sobrevive a renombrados.
    /// </summary>
    public static string HeaderLine(
        string lineId)
    {
        return $"header.line.{lineId}";
    }
}
