using Renderset.Core.Definitions;

namespace Renderset.Core.Reports.Inference;

/// <summary>
/// Rehace la definición de un report cuando cambia la estructura de sus datos
/// (un campo pasa de la cabecera a las líneas, aparece un nivel nuevo...)
/// sin perder lo que ya estaba.
///
/// La estructura nueva manda: secciones, campos y columnas salen de
/// <see cref="ReportDefinitionFactory"/> con el esquema nuevo, y los tipos
/// también (el mapping es quien sabe de qué tipo es cada dato). De la
/// definición anterior se conserva, para lo que sigue existiendo con el mismo
/// id: nombres, etiquetas, visibilidad por defecto, orden y anchos. Cabecera
/// y pie no dependen de los datos y se quedan tal cual.
///
/// Lo que se configuró en los presets (sparse, por id) sigue aplicando a lo
/// que no se ha movido; un campo movido cambia de id (su ruta) y empieza con
/// los valores por defecto en su sitio nuevo.
/// </summary>
public static class ReportDefinitionMerger
{
    public static ReportDefinition Merge(
        ReportDefinition current,
        ReportDefinition fresh)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(fresh);

        var sections = new Dictionary<string, ReportSectionDefinition>(StringComparer.OrdinalIgnoreCase);
        var fields = new Dictionary<string, (ReportFieldDefinition Field, string SectionId)>(StringComparer.OrdinalIgnoreCase);
        var columns = new Dictionary<string, (ReportColumnDefinition Column, string TableId)>(StringComparer.OrdinalIgnoreCase);
        var tables = new Dictionary<string, ReportTableDefinition>(StringComparer.OrdinalIgnoreCase);

        void Index(IEnumerable<ReportSectionDefinition> list)
        {
            foreach (var section in list)
            {
                sections.TryAdd(section.Id, section);

                foreach (var field in section.Fields)
                    fields.TryAdd(field.Id, (field, section.Id));

                if (section.Table is not null)
                {
                    tables.TryAdd(section.Table.Id, section.Table);

                    foreach (var column in section.Table.Columns)
                        columns.TryAdd(column.Id, (column, section.Table.Id));
                }

                Index(section.Sections);
            }
        }

        Index(current.Sections);

        List<ReportSectionDefinition> MergeSections(
            IReadOnlyList<ReportSectionDefinition> list)
        {
            var next = NextOrder(list.Select(x => sections.TryGetValue(x.Id, out var old) ? old.Order : (int?)null));

            return list
                .Select(section =>
                {
                    sections.TryGetValue(section.Id, out var old);

                    return new ReportSectionDefinition
                    {
                        Id = section.Id,
                        Name = old?.Name ?? section.Name,
                        Order = old?.Order ?? next(),
                        VisibleByDefault = old?.VisibleByDefault ?? section.VisibleByDefault,
                        DataPath = section.DataPath,
                        Fields = MergeFields(section.Id, section.Fields),
                        Table = MergeTable(section.Table),
                        Sections = MergeSections(section.Sections)
                    };
                })
                .ToList();
        }

        List<ReportFieldDefinition> MergeFields(
            string sectionId,
            IReadOnlyList<ReportFieldDefinition> list)
        {
            // El orden de antes sólo vale dentro de la misma sección: un campo
            // que llega de otra se coloca al final.
            int? SameSectionOrder(ReportFieldDefinition field) =>
                fields.TryGetValue(field.Id, out var old) &&
                string.Equals(old.SectionId, sectionId, StringComparison.OrdinalIgnoreCase)
                    ? old.Field.Order
                    : null;

            var next = NextOrder(list.Select(SameSectionOrder));

            return list
                .Select(field =>
                {
                    fields.TryGetValue(field.Id, out var old);

                    return new ReportFieldDefinition
                    {
                        Id = field.Id,
                        Label = old.Field?.Label ?? field.Label,
                        DataPath = field.DataPath,
                        Order = SameSectionOrder(field) ?? next(),
                        VisibleByDefault = old.Field?.VisibleByDefault ?? field.VisibleByDefault,
                        Type = field.Type
                    };
                })
                .ToList();
        }

        ReportTableDefinition? MergeTable(
            ReportTableDefinition? table)
        {
            if (table is null)
                return null;

            tables.TryGetValue(table.Id, out var oldTable);

            int? SameTableOrder(ReportColumnDefinition column) =>
                columns.TryGetValue(column.Id, out var old) &&
                string.Equals(old.TableId, table.Id, StringComparison.OrdinalIgnoreCase)
                    ? old.Column.Order
                    : null;

            var next = NextOrder(table.Columns.Select(SameTableOrder));

            return new ReportTableDefinition
            {
                Id = table.Id,
                Name = oldTable?.Name ?? table.Name,
                DataPath = table.DataPath,
                Columns = table.Columns
                    .Select(column =>
                    {
                        columns.TryGetValue(column.Id, out var old);

                        return new ReportColumnDefinition
                        {
                            Id = column.Id,
                            Label = old.Column?.Label ?? column.Label,
                            DataPath = column.DataPath,
                            Order = SameTableOrder(column) ?? next(),
                            VisibleByDefault = old.Column?.VisibleByDefault ?? column.VisibleByDefault,
                            Width = old.Column?.Width ?? column.Width,
                            Type = column.Type
                        };
                    })
                    .ToList()
            };
        }

        return new ReportDefinition
        {
            Id = current.Id,
            Name = current.Name,
            Version = current.Version + 1,
            Header = current.Header ?? fresh.Header,
            Footer = current.Footer ?? fresh.Footer,
            Sections = MergeSections(fresh.Sections)
        };
    }

    /// <summary>
    /// Órdenes para lo nuevo: de diez en diez detrás del mayor que se
    /// conserva, en el orden en que llega.
    /// </summary>
    private static Func<int> NextOrder(
        IEnumerable<int?> kept)
    {
        var current = kept.Where(x => x is not null).Select(x => x!.Value).DefaultIfEmpty(0).Max();

        return () =>
        {
            current += 10;
            return current;
        };
    }
}
