namespace Renderset.Core.Mapping;

public enum DataMappingNodeKind
{
    /// <summary>
    /// Objeto único dentro del documento: customer, shipping, summary.
    /// </summary>
    Object,

    /// <summary>
    /// Colección: lines[], materials[]. Cada valor distinto de sus claves
    /// produce un elemento.
    /// </summary>
    Collection
}
