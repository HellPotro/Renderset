using System.Text.Json;
using Renderset.Core.DataSources;

namespace Renderset.Core.Mapping;

public interface IHierarchyBuilder
{
    /// <summary>
    /// Convierte un resultado plano en una secuencia de documentos.
    ///
    /// Secuencia, no un único documento: una consulta SQL de facturas
    /// normalmente devuelve N facturas, y emitir de una en una es lo que
    /// permite generar 5.000 PDFs sin construir antes un JSON de 5.000
    /// facturas en memoria.
    /// </summary>
    IAsyncEnumerable<JsonElement> BuildAsync(
        IDataSetReader reader,
        DataMapping mapping,
        HierarchyBuilderOptions? options = null,
        CancellationToken cancellationToken = default);
}
