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

    public static string TextBlockTitle(
        string blockId)
    {
        return $"block.{blockId}.title";
    }

    public static string TextBlockText(
        string blockId)
    {
        return $"block.{blockId}.text";
    }
}
