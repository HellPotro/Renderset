using Renderset.Core.Definitions;
using Renderset.Core.Resolved;

namespace Renderset.Core.Services;

/// <summary>
/// Convierte las banderas de la configuración en filas y anchos concretos.
///
/// Está fuera del resolver porque es la única parte del layout del cuerpo que
/// tiene reglas propias y conviene poder probar sola. Las dos operaciones que
/// la componen son públicas porque el editor necesita enseñar exactamente el
/// mismo reparto que acabará viendo el documento: si cada uno lo calculase
/// por su cuenta, tarde o temprano dirían cosas distintas.
/// </summary>
public static class ReportBodyLayout
{
    /// <summary>
    /// Rejilla de referencia. Doce divide entre 2, 3, 4 y 6, que son los
    /// repartos que de verdad se usan en un documento.
    /// </summary>
    public const int Columns = 12;

    /// <summary>
    /// Asigna <see cref="ResolvedReportBodyItem.Row"/> y
    /// <see cref="ResolvedReportBodyItem.Span"/> a una lista YA ordenada.
    ///
    /// Si no hay configuración, cada elemento se queda en su propia fila a
    /// ancho completo, que es como se comportaba el cuerpo antes de existir
    /// esto.
    /// </summary>
    public static void Apply(
        IReadOnlyList<ResolvedReportBodyItem> items,
        IReadOnlyCollection<ReportBodyItemConfiguration>? configuration)
    {
        ArgumentNullException.ThrowIfNull(items);

        var settings = BuildSettings(configuration);

        var rows =
            BuildRows(
                items
                    .Select(x => SettingFor(settings, x.Id)?.SameRow)
                    .ToList());

        for (var row = 0; row < rows.Count; row++)
        {
            var members = rows[row];

            var spans =
                DistributeSpans(
                    members
                        .Select(index => SettingFor(settings, items[index].Id)?.Span)
                        .ToList());

            for (var position = 0; position < members.Count; position++)
            {
                var item = items[members[position]];

                item.Row = row + 1;
                item.Span = spans[position];
            }
        }
    }


    /// <summary>
    /// Reparte una secuencia de banderas en filas y devuelve, por fila, las
    /// posiciones que la componen.
    ///
    /// El primer elemento nunca continúa una fila anterior aunque venga
    /// marcado: pasa al borrar el elemento que tenía encima, y no es motivo
    /// para dejar el documento inconsistente.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<int>> BuildRows(
        IReadOnlyList<bool?> sameRow)
    {
        ArgumentNullException.ThrowIfNull(sameRow);

        var rows = new List<List<int>>();

        for (var index = 0; index < sameRow.Count; index++)
        {
            var continuesRow =
                index > 0 &&
                sameRow[index] == true;

            if (!continuesRow || rows.Count == 0)
                rows.Add([]);

            rows[^1].Add(index);
        }

        return rows;
    }


    /// <summary>
    /// Reparte el ancho entre los elementos de una fila. Lo que no está
    /// configurado se reparte a partes iguales, y el resto de la división se
    /// da a los primeros para que la fila sume siempre doce: tres elementos
    /// salen 4-4-4 y cinco salen 3-3-2-2-2.
    /// </summary>
    public static IReadOnlyList<int> DistributeSpans(
        IReadOnlyList<int?> requested)
    {
        ArgumentNullException.ThrowIfNull(requested);

        if (requested.Count == 0)
            return [];

        var share = Columns / requested.Count;
        var remainder = Columns % requested.Count;

        var spans = new int[requested.Count];

        for (var index = 0; index < requested.Count; index++)
        {
            var fallback =
                Math.Max(
                    1,
                    share + (index < remainder ? 1 : 0));

            spans[index] =
                Math.Clamp(
                    requested[index] ?? fallback,
                    1,
                    Columns);
        }

        return spans;
    }


    /// <summary>
    /// Se indexa a mano y no con ToDictionary porque un body heredado puede
    /// traer ids repetidos, y ahí interesa quedarse con el primero en vez de
    /// reventar al resolver.
    /// </summary>
    private static Dictionary<string, ReportBodyItemConfiguration> BuildSettings(
        IReadOnlyCollection<ReportBodyItemConfiguration>? configuration)
    {
        var settings =
            new Dictionary<string, ReportBodyItemConfiguration>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var item in configuration ?? [])
        {
            if (string.IsNullOrWhiteSpace(item.Id))
                continue;

            settings.TryAdd(item.Id, item);
        }

        return settings;
    }


    private static ReportBodyItemConfiguration? SettingFor(
        IReadOnlyDictionary<string, ReportBodyItemConfiguration> settings,
        string id) =>
        settings.TryGetValue(id, out var setting)
            ? setting
            : null;
}
