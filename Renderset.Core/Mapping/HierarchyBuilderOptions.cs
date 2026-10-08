namespace Renderset.Core.Mapping;

public sealed class HierarchyBuilderOptions
{
    /// <summary>
    /// Si el origen viene ordenado por las claves de la raíz, los documentos
    /// se emiten según se leen y la memoria usada es la de un documento.
    ///
    /// Si no lo está, hay que acumular todas las filas antes de poder cerrar
    /// ningún documento. Por eso está en false por defecto: es lo seguro. Con
    /// consultas grandes, añade ORDER BY a la consulta y ponlo en true.
    /// </summary>
    public bool AssumeOrderedByKey { get; set; }

    /// <summary>
    /// Corta la lectura si el origen devuelve más filas de las esperadas.
    /// Cero significa sin límite.
    /// </summary>
    public int MaxRows { get; set; }

    /// <summary>
    /// Una fila de LEFT JOIN sin detalle trae todas las columnas del hijo a
    /// null. Con esto activado no genera un elemento vacío en la colección,
    /// que es lo que uno espera. Desactívalo sólo si tus datos tienen
    /// elementos legítimamente vacíos.
    /// </summary>
    public bool SkipEmptyChildRows { get; set; } = true;

    /// <summary>
    /// Quita los espacios del final de los textos. Las columnas CHAR de SQL
    /// Server llegan rellenas ("Ingles    ") y en un documento eso descuadra
    /// tablas y comparaciones.
    /// </summary>
    public bool TrimStrings { get; set; } = true;

    /// <summary>
    /// Qué hacer con un valor que no encaja en el tipo del campo (un texto
    /// en una columna marcada como número). Por defecto se conserva como
    /// texto: un dato raro en una referencia no puede impedir emitir un
    /// albarán. Con true, lanza <see cref="DataMappingValueException"/>.
    /// </summary>
    public bool StrictTypes { get; set; }
}
