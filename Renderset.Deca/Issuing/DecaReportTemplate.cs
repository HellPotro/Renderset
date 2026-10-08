using Renderset.Core.Configurations;
using Renderset.Core.Definitions;
using Renderset.Core.Tenancy;
using Renderset.Core.Themes;

namespace Renderset.Deca.Issuing;

/// <summary>
/// Plantilla del DeCA sobre el motor de reports: una definición y una
/// configuración hechas en código, no guardadas. Así el documento sale del
/// mismo resolver y el mismo renderer que cualquier report (QR, pie con
/// "Página 1 de 2", tipografías incrustadas) y nadie puede quitarle un dato
/// obligatorio desde el diseñador: no hay preset que editar.
///
/// Del tenant sólo se toma la marca (logo y colores de "Compartir").
///
/// Las rutas de datos son las de <see cref="DecaReportData"/>.
/// </summary>
public static class DecaReportTemplate
{
    public const string ReportId = "deca";

    public static ReportDefinition Definition { get; } = BuildDefinition();

    private static ReportDefinition BuildDefinition() =>
        new()
        {
            Id = ReportId,
            Name = "Documento de control administrativo",
            Version = 1,

            Header = new ReportHeaderDefinition
            {
                Title = "Documento de control administrativo",
                Subtitle = "Nº {{data.numero}} · Versión {{data.version}} · Emitido el {{data.emitido}}",
                AllowLogo = true
            },

            Sections =
            [
                Fields(
                    "cargador",
                    "Cargador contractual",
                    10,
                    ("cargador.razonSocial", "Razón social"),
                    ("cargador.nif", "NIF"),
                    ("cargador.domicilio", "Domicilio")),

                Fields(
                    "transportista",
                    "Transportista efectivo",
                    20,
                    ("transportista.razonSocial", "Razón social"),
                    ("transportista.nif", "NIF")),

                Fields(
                    "transporte",
                    "Transporte",
                    30,
                    ("transporte.fecha", "Fecha del transporte"),
                    ("transporte.inicio", "Inicio previsto"),
                    ("transporte.tractora", "Vehículo tractor o rígido"),
                    ("transporte.remolque", "Remolque / semirremolque"),
                    ("transporte.autorizacionEspecial", "Autorización especial de circulación"),
                    ("transporte.referencia", "Referencia")),

                new ReportSectionDefinition
                {
                    Id = "envios",
                    Name = "Envíos",
                    Order = 40,
                    VisibleByDefault = true,
                    Fields = [],
                    Table = new ReportTableDefinition
                    {
                        Id = "envios.table",
                        Name = "Envíos",
                        DataPath = "envios",
                        Columns =
                        [
                            Column("envios.numero", "Nº", "numero", 10, 5),
                            Column("envios.origen", "Origen", "origen", 20, 24),
                            Column("envios.destino", "Destino", "destino", 30, 24),
                            Column("envios.mercancia", "Naturaleza de la mercancía", "mercancia", 40, 22),
                            Column("envios.cantidad", "Peso / magnitud", "cantidad", 50, 12),
                            Column("envios.notas", "Notas", "notas", 60, 13)
                        ]
                    }
                },

                Fields(
                    "observaciones",
                    "Observaciones y reservas",
                    50,
                    ("observaciones.texto", "Observaciones")),

                Fields(
                    "emision",
                    "Emisión",
                    60,
                    ("emision.numero", "Número"),
                    ("emision.fechaHora", "Fecha y hora de emisión"),
                    ("emision.version", "Versión"),
                    ("emision.enlace", "Enlace de descarga"))
            ],

            Footer = new ReportFooterDefinition
            {
                Text =
                    "DeCA {{data.numero}} · Orden FOM/2861/2012 y Resolución de 5 de junio de 2026. " +
                    "Documento electrónico: la descarga del código QR es la versión vigente.",
                ShowGenerationDate = false,
                AllowPageNumber = true
            }
        };

    /// <summary>
    /// Configuración fija: cuadrículas, QR que descarga el PDF y título en
    /// banda propia (logo, datos y QR arriba sin apelotonarse).
    /// </summary>
    public static ReportConfiguration Configuration(
        TenantBranding? branding)
    {
        var logo = branding?.LogoUrl;

        return new ReportConfiguration
        {
            Id = ReportId,
            ReportId = ReportId,
            Name = "DeCA",

            Header = new ReportHeaderConfiguration
            {
                Visible = true,
                ShowTitle = true,
                ShowSubtitle = true,
                ShowLogo = !string.IsNullOrWhiteSpace(logo),
                LogoUrl = logo,
                LogoMaxHeight = 56,
                Layout = ReportHeaderLayout.LogoLeft,
                TitlePlacement = ReportHeaderTitlePlacement.Below,
                ShowDivider = true,

                // El QR descarga el PDF directamente (requisito de la norma).
                // En el DeCA {pdfUrl} es el enlace corto /q/{código}.
                ShowQr = true,
                QrContent = Core.Rendering.ReportQrContent.PdfUrlTemplate,
                QrSize = 104
            },

            Sections =
            [
                Grid("cargador", 3),
                Grid("transportista", 2),
                Grid("transporte", 3),
                Grid("observaciones", 1),
                Grid("emision", 2)
            ],

            Footer = new ReportFooterConfiguration
            {
                Visible = true,
                ShowPageNumber = true,
                ShowGenerationDate = false
            }
        };
    }

    public static ReportTheme Theme(
        TenantBranding? branding)
    {
        var theme = new ReportTheme
        {
            Id = ReportId,
            Name = "DeCA"
        };

        if (branding is not null)
        {
            theme.PrimaryColor = branding.PrimaryColor;
            theme.TitleColor = branding.PrimaryColor;
            theme.SectionTextColor = branding.PrimaryColor;
            theme.TableHeaderTextColor = branding.PrimaryColor;
            theme.SecondaryColor = branding.SecondaryColor;
            theme.SectionBackgroundColor = branding.SecondaryColor;
            theme.TableHeaderBackgroundColor = branding.SecondaryColor;
        }

        return theme;
    }

    private static ReportSectionDefinition Fields(
        string id,
        string name,
        int order,
        params (string Id, string Label)[] fields) =>
        new()
        {
            Id = id,
            Name = name,
            Order = order,
            VisibleByDefault = true,
            Fields =
                fields
                    .Select((field, index) => new ReportFieldDefinition
                    {
                        Id = field.Id,
                        Label = field.Label,
                        DataPath = field.Id,
                        Order = (index + 1) * 10,
                        VisibleByDefault = true,
                        Type = ReportFieldType.Text
                    })
                    .ToList()
        };

    private static ReportColumnDefinition Column(
        string id,
        string label,
        string path,
        int order,
        decimal width) =>
        new()
        {
            Id = id,
            Label = label,
            DataPath = path,
            Order = order,
            Width = width,
            VisibleByDefault = true,
            Type = ReportFieldType.Text
        };

    private static ReportSectionConfiguration Grid(
        string sectionId,
        int columns) =>
        new()
        {
            SectionId = sectionId,
            Layout = columns == 1 ? ReportSectionLayout.List : ReportSectionLayout.Grid,
            Columns = columns == 1 ? null : columns
        };
}
