using System.Text.Json;
using System.Text.Json.Serialization;
using Renderset.Core.Configurations;
using Renderset.Core.Localization;
using Renderset.Core.Rendering;
using Renderset.Core.Resolved;
using Renderset.Core.Services;
using Renderset.Core.Tenancy;
using Renderset.Core.Themes;

namespace Renderset.Deca.Issuing;

/// <summary>
/// Lo que ningún diseño puede quitar a un DeCA.
///
/// El diseño se edita con el ReportEditor de RenderSet, que deja ocultar
/// cualquier campo. Aquí se deshace lo que dejaría un DeCA incompleto, en
/// dos pasos:
///
///   1. <see cref="Enforce"/>, antes de resolver: quita las ocultaciones
///      de los datos obligatorios y deja el QR de descarga del PDF.
///   2. <see cref="Missing"/>, sobre el documento ya resuelto: lo que de
///      verdad saldrá en el papel. Si falta algo, el diseño no se guarda y,
///      al emitir, se usa el base.
///
/// Se puede cambiar todo lo demás: etiquetas, orden, disposición, columnas,
/// anchos, colores, tipografías, logo, título, pie, secciones y bloques
/// añadidos.
/// </summary>
public static class DecaTemplateGuard
{
    /// <summary>
    /// Campos obligatorios por la norma, con el nombre que se enseña si
    /// faltan.
    /// </summary>
    public static IReadOnlyList<(string Id, string Name)> RequiredFields { get; } =
    [
        ("cargador.razonSocial", "Razón social del cargador"),
        ("cargador.nif", "NIF del cargador"),
        ("cargador.domicilio", "Domicilio del cargador"),
        ("transportista.razonSocial", "Razón social del transportista"),
        ("transportista.nif", "NIF del transportista"),
        ("transporte.fecha", "Fecha del transporte"),
        ("transporte.tractora", "Matrícula del tractor o rígido"),
        ("transporte.remolque", "Matrícula del remolque"),
        ("transporte.autorizacionEspecial", "Autorización especial"),
        ("observaciones.texto", "Observaciones"),
        ("emision.fechaHora", "Fecha y hora de emisión")
    ];

    public static IReadOnlyList<(string Id, string Name)> RequiredColumns { get; } =
    [
        ("envios.origen", "Origen de cada envío"),
        ("envios.destino", "Destino de cada envío"),
        ("envios.mercancia", "Naturaleza de la mercancía"),
        ("envios.cantidad", "Peso o magnitud")
    ];

    /// <summary>
    /// Secciones que no se pueden ocultar: llevan los obligatorios.
    /// </summary>
    public static IReadOnlyList<(string Id, string Name)> RequiredSectionList { get; } =
    [
        ("cargador", "La sección Cargador contractual"),
        ("transportista", "La sección Transportista efectivo"),
        ("transporte", "La sección Transporte"),
        ("envios", "La tabla de envíos"),
        ("observaciones", "La sección Observaciones"),
        ("emision", "La sección Emisión")
    ];

    /// <summary>
    /// Lo que se enseña en el diseñador cuando se intenta quitar algo.
    /// </summary>
    public const string LockReason =
        "lo exige la normativa del DeCA (Orden FOM/2861/2012) y no se puede quitar.";

    public const string QrName = "Código QR de descarga del PDF";

    private static readonly HashSet<string> RequiredSections =
        RequiredSectionList.Select(x => x.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> RequiredFieldIds =
        RequiredFields.Select(x => x.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> RequiredColumnIds =
        RequiredColumns.Select(x => x.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// QR más pequeño que se admite: por debajo, impreso, cuesta leerlo.
    /// </summary>
    public const int MinQrSize = 80;

    private static readonly JsonSerializerOptions CloneOptions =
        new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() }
        };

    /// <summary>
    /// Copia del diseño con lo obligatorio restaurado. No toca el original.
    /// </summary>
    public static ReportConfiguration Enforce(
        ReportConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var copy =
            JsonSerializer.Deserialize<ReportConfiguration>(
                JsonSerializer.Serialize(configuration, CloneOptions),
                CloneOptions)!;

        copy.Id = DecaReportTemplate.ReportId;
        copy.ReportId = DecaReportTemplate.ReportId;

        copy.Header ??= new ReportHeaderConfiguration();
        copy.Header.Visible = true;
        copy.Header.ShowQr = true;
        copy.Header.QrContent = ReportQrContent.PdfUrlTemplate;

        if (copy.Header.QrSize is { } size && size < MinQrSize)
            copy.Header.QrSize = MinQrSize;

        // Un bloque reutilizable de cabecera vendría de RenderSet, que el
        // DeCA no lee: se quita para que no dependa de nada de fuera.
        copy.Header.BlockId = null;

        if (copy.Footer is not null)
            copy.Footer.BlockId = null;

        Restore(copy.Sections);

        return copy;
    }

    private static void Restore(
        List<ReportSectionConfiguration> sections)
    {
        foreach (var section in sections)
        {
            if (RequiredSections.Contains(section.SectionId) && section.Visible == false)
                section.Visible = null;

            foreach (var field in section.Fields)
            {
                if (RequiredFieldIds.Contains(field.FieldId) && field.Visible == false)
                    field.Visible = null;
            }

            if (section.Table is { } table)
            {
                foreach (var column in table.Columns)
                {
                    if (RequiredColumnIds.Contains(column.ColumnId) && column.Visible == false)
                        column.Visible = null;
                }
            }

            Restore(section.Sections);
        }
    }

    /// <summary>
    /// Lo obligatorio que no saldría en el documento resuelto, por nombre.
    /// Vacío = el diseño vale.
    /// </summary>
    public static IReadOnlyList<string> Missing(
        ResolvedReportDefinition resolved)
    {
        ArgumentNullException.ThrowIfNull(resolved);

        var fields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Collect(ResolvedReportSection section)
        {
            if (!section.Visible)
                return;

            foreach (var field in section.Fields.Where(x => x.Visible))
                fields.Add(field.Id);

            if (section.Table is { } table)
            {
                foreach (var column in table.Columns.Where(x => x.Visible))
                    columns.Add(column.Id);
            }

            foreach (var child in section.Sections)
                Collect(child);
        }

        // Lo que se pinta es el cuerpo, no la lista de secciones.
        foreach (var item in resolved.Body)
        {
            if (item.Section is { } section)
                Collect(section);
        }

        var missing =
            RequiredFields
                .Where(x => !fields.Contains(x.Id))
                .Select(x => x.Name)
                .Concat(
                    RequiredColumns
                        .Where(x => !columns.Contains(x.Id))
                        .Select(x => x.Name))
                .ToList();

        if (resolved.Header is not { Visible: true, ShowQr: true } header ||
            !string.Equals(header.QrContent, ReportQrContent.PdfUrlTemplate, StringComparison.OrdinalIgnoreCase))
        {
            missing.Add(QrName);
        }

        return missing;
    }

    /// <summary>
    /// Lo que se usa para pintar: el diseño del tenant ya corregido o, si no
    /// tiene o no vale, el base con su marca.
    /// </summary>
    public sealed record Design(
        ResolvedReportDefinition Resolved,
        ReportTheme Theme,
        int TemplateVersion,
        IReadOnlyList<string> Rejected);

    public static Design Resolve(
        DecaTemplate? template,
        TenantBranding? branding)
    {
        var resolver = new ReportConfigurationResolver();

        if (template is { Configuration: { } configuration })
        {
            var resolved =
                resolver.Resolve(
                    DecaReportTemplate.Definition,
                    WithBranding(Enforce(configuration), branding),
                    bodyBlocks: null,
                    new ReportTextCatalog(
                        new Dictionary<string, string>(template.Texts, StringComparer.OrdinalIgnoreCase),
                        "es-ES"));

            var missing = Missing(resolved);

            if (missing.Count == 0)
            {
                return new Design(
                    resolved,
                    template.Theme ?? DecaReportTemplate.Theme(branding),
                    template.Version,
                    []);
            }

            return Base(branding, missing);
        }

        return Base(branding, []);
    }

    /// <summary>
    /// El logo del DeCA es el de la marca del tenant (Marca en el portal,
    /// Página pública en RenderSet) mientras el diseño no ponga otro: así
    /// cambiar el logo de la empresa cambia los DeCA siguientes sin tocar
    /// el diseño. Modifica y devuelve la misma configuración.
    /// </summary>
    public static ReportConfiguration WithBranding(
        ReportConfiguration configuration,
        TenantBranding? branding)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (string.IsNullOrWhiteSpace(branding?.LogoUrl))
            return configuration;

        configuration.Header ??= new ReportHeaderConfiguration();

        if (string.IsNullOrWhiteSpace(configuration.Header.LogoUrl))
            configuration.Header.LogoUrl = branding.LogoUrl;

        return configuration;
    }

    /// <summary>
    /// Lo contrario, al guardar: un logo igual al de la marca no se guarda
    /// en el diseño (seguiría al de la marca), y un logo apagado porque la
    /// empresa no tenía ninguno tampoco (se encenderá cuando lo tenga).
    /// </summary>
    public static ReportConfiguration WithoutBranding(
        ReportConfiguration configuration,
        TenantBranding? branding)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (configuration.Header is not { } header)
            return configuration;

        var brandLogo = branding?.LogoUrl;

        if (!string.IsNullOrWhiteSpace(header.LogoUrl) &&
            string.Equals(header.LogoUrl.Trim(), brandLogo, StringComparison.Ordinal))
        {
            header.LogoUrl = null;
        }

        if (string.IsNullOrWhiteSpace(brandLogo) &&
            string.IsNullOrWhiteSpace(header.LogoUrl) &&
            header.ShowLogo == false)
        {
            header.ShowLogo = null;
        }

        return configuration;
    }

    private static Design Base(
        TenantBranding? branding,
        IReadOnlyList<string> rejected) =>
        new(
            new ReportConfigurationResolver().Resolve(
                DecaReportTemplate.Definition,
                DecaReportTemplate.Configuration(branding)),
            DecaReportTemplate.Theme(branding),
            DecaTemplate.BaseVersion,
            rejected);
}
