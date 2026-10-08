using System.Text;

namespace Renderset.Core.Themes;

/// <summary>
/// Variables CSS del tema, tal como las lee report-document.css. Las usan el
/// renderer del documento, el preview de bloques y la pantalla de temas, así
/// que un color nuevo se añade aquí una sola vez.
/// </summary>
public static class ReportThemeCss
{
    public static string Variables(
        ReportTheme? theme)
    {
        theme ??= new ReportTheme();

        var css = new StringBuilder();

        void Add(string name, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                css.Append("--report-").Append(name).Append(':').Append(value.Trim()).Append(';');
        }

        Add("primary", theme.PrimaryColor);
        Add("secondary", theme.SecondaryColor);
        Add("text", theme.TextColor);
        Add("muted", theme.MutedTextColor);
        Add("border", theme.BorderColor);
        Add("section-bg", theme.SectionBackgroundColor);
        Add("section-text", theme.SectionTextColor);
        Add("table-header-bg", theme.TableHeaderBackgroundColor);
        Add("table-header-text", theme.TableHeaderTextColor);
        Add("background", theme.BackgroundColor);
        Add("font", theme.FontFamily);

        // Opcionales: sin valor no se emite la variable y la hoja usa su
        // valor de reserva, que es el aspecto anterior a que existieran.
        Add("title", theme.TitleColor);
        Add("table-stripe", theme.TableStripeColor);
        Add("field-label-bg", theme.FieldLabelBackgroundColor);

        return css.ToString();
    }
}
