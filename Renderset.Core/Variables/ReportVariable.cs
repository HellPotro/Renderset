namespace Renderset.Core.Variables;

public sealed class ReportVariable
{
    public required string Key { get; set; }

    public string? Value { get; set; }

    public string? Description { get; set; }

    public ReportVariableType Type { get; set; } =
        ReportVariableType.Text;

    /// <summary>
    /// Si el valor cambia según el idioma del documento.
    /// 
    /// Es una decisión explícita y no algo deducido del tipo: el nombre
    /// comercial de la empresa suele ser el mismo en todos los idiomas, pero
    /// un lema o un pie legal no. Y al revés, un color o una URL nunca deben
    /// acabar en el traductor automático aunque sean texto.
    /// 
    /// Cuando está activa, el valor se busca en el diccionario común del
    /// tenant bajo la clave variable.{key}, con el valor de aquí como
    /// reserva.
    /// </summary>
    public bool Translatable { get; set; }
}