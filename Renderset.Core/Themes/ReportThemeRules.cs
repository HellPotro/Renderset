using System.Text;
using System.Text.RegularExpressions;

namespace Renderset.Core.Themes;

/// <summary>
/// Validación de temas.
///
/// Los valores acaban dentro de un atributo style del documento: un color
/// que no sea hexadecimal podría colar otras propiedades CSS ("red;
/// background:url(...)"). Por eso sólo se aceptan #RGB / #RRGGBB y una
/// familia tipográfica sin caracteres de control de CSS.
/// </summary>
public static partial class ReportThemeRules
{
    public const int MaxIdLength = 64;

    public const int MaxNameLength = 100;

    public const int MaxFontFamilyLength = 200;

    [GeneratedRegex("^#([0-9a-fA-F]{3}|[0-9a-fA-F]{6})$")]
    private static partial Regex HexColor();

    [GeneratedRegex("^[a-z0-9][a-z0-9-]*$")]
    private static partial Regex Slug();

    public static bool IsColor(string? value) =>
        value is not null && HexColor().IsMatch(value.Trim());

    public static bool IsFontFamily(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= MaxFontFamilyLength &&
        value.All(c => !char.IsControl(c) && c is not (';' or '{' or '}' or '<' or '>' or '(' or ')' or '\\' or ':'));

    public static IReadOnlyList<string> Validate(
        ReportTheme? theme)
    {
        var errors = new List<string>();

        if (theme is null)
        {
            errors.Add("Falta el tema.");
            return errors;
        }

        if (string.IsNullOrWhiteSpace(theme.Id) ||
            theme.Id.Length > MaxIdLength ||
            !Slug().IsMatch(theme.Id))
        {
            errors.Add("El identificador sólo admite minúsculas, números y guiones (máx. 64).");
        }

        if (string.IsNullOrWhiteSpace(theme.Name))
            errors.Add("El nombre es obligatorio.");
        else if (theme.Name.Trim().Length > MaxNameLength)
            errors.Add($"El nombre admite como mucho {MaxNameLength} caracteres.");

        void Required(string label, string? value)
        {
            if (!IsColor(value))
                errors.Add($"{label}: tiene que ser un color hexadecimal (#1A2B3C).");
        }

        void Optional(string label, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value) && !IsColor(value))
                errors.Add($"{label}: tiene que ser un color hexadecimal (#1A2B3C) o quedar vacío.");
        }

        Required("Color principal", theme.PrimaryColor);
        Required("Color secundario", theme.SecondaryColor);
        Required("Texto", theme.TextColor);
        Required("Texto secundario", theme.MutedTextColor);
        Required("Bordes", theme.BorderColor);
        Required("Fondo de sección", theme.SectionBackgroundColor);
        Required("Texto de sección", theme.SectionTextColor);
        Required("Cabecera de tabla", theme.TableHeaderBackgroundColor);
        Required("Texto de cabecera de tabla", theme.TableHeaderTextColor);
        Required("Fondo del documento", theme.BackgroundColor);
        Optional("Título", theme.TitleColor);
        Optional("Bandas de tabla", theme.TableStripeColor);
        Optional("Fondo de etiquetas", theme.FieldLabelBackgroundColor);

        if (!IsFontFamily(theme.FontFamily))
            errors.Add("La tipografía no es válida.");

        return errors;
    }

    /// <summary>
    /// Copia limpia para guardar: recorta espacios y deja nulos los
    /// opcionales vacíos.
    /// </summary>
    public static ReportTheme Normalize(
        ReportTheme theme)
    {
        var copy = theme.Clone();

        copy.Id = theme.Id.Trim();
        copy.Name = theme.Name.Trim();
        copy.PrimaryColor = theme.PrimaryColor.Trim();
        copy.SecondaryColor = theme.SecondaryColor.Trim();
        copy.TextColor = theme.TextColor.Trim();
        copy.MutedTextColor = theme.MutedTextColor.Trim();
        copy.BorderColor = theme.BorderColor.Trim();
        copy.SectionBackgroundColor = theme.SectionBackgroundColor.Trim();
        copy.SectionTextColor = theme.SectionTextColor.Trim();
        copy.TableHeaderBackgroundColor = theme.TableHeaderBackgroundColor.Trim();
        copy.TableHeaderTextColor = theme.TableHeaderTextColor.Trim();
        copy.BackgroundColor = theme.BackgroundColor.Trim();
        copy.FontFamily = theme.FontFamily.Trim();
        copy.TitleColor = NullIfBlank(theme.TitleColor);
        copy.TableStripeColor = NullIfBlank(theme.TableStripeColor);
        copy.FieldLabelBackgroundColor = NullIfBlank(theme.FieldLabelBackgroundColor);

        return copy;
    }

    /// <summary>
    /// Identificador a partir del nombre: "ORAN compacto" → "oran-compacto".
    /// </summary>
    public static string SlugFrom(
        string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "tema";

        var normalized =
            name.Trim()
                .ToLowerInvariant()
                .Normalize(NormalizationForm.FormD);

        var builder = new StringBuilder();

        foreach (var c in normalized)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) ==
                System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsAsciiLetterOrDigit(c))
                builder.Append(c);
            else if (builder.Length > 0 && builder[^1] != '-')
                builder.Append('-');
        }

        var slug = builder.ToString().Trim('-');

        if (slug.Length > MaxIdLength)
            slug = slug[..MaxIdLength].Trim('-');

        return slug.Length == 0 ? "tema" : slug;
    }

    private static string? NullIfBlank(
        string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
}
