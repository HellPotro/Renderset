using System.Text.Json;
using Renderset.Core.Resolved;
using Renderset.Core.Themes;

namespace Renderset.Core.Rendering;

/// <summary>
/// Convierte un informe ya resuelto en el HTML del documento.
///
/// Es una interfaz y no una clase de Core porque la implementación usa los
/// componentes Blazor de Renderset.Blazor, que son los mismos que pinta el
/// preview. Es la única forma de garantizar que lo que se ve en el diseñador
/// es lo que sale por API.
/// </summary>
public interface IReportDocumentRenderer
{
    Task<string> RenderHtmlAsync(
        ResolvedReportDefinition report,
        JsonElement data,
        ReportTheme theme,
        string? title = null,
        bool includeToolbar = false,
        bool includeDocumentData = false,
        CancellationToken cancellationToken = default);
}
