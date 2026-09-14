namespace Renderset.Core.Themes;

public sealed class ReportTheme
{
    public string Id { get; set; } = "default";

    public string Name { get; set; } = "Default";

    public string PrimaryColor { get; set; } = "#00285A";

    public string SecondaryColor { get; set; } = "#E8EFF3";

    public string TextColor { get; set; } = "#25313A";

    public string MutedTextColor { get; set; } = "#6D7B83";

    public string BorderColor { get; set; } = "#D8E1E7";

    public string SectionBackgroundColor { get; set; } = "#E8EFF3";

    public string SectionTextColor { get; set; } = "#00285A";

    public string TableHeaderBackgroundColor { get; set; } = "#E8EFF3";

    public string TableHeaderTextColor { get; set; } = "#00285A";

    public string BackgroundColor { get; set; } = "#FFFFFF";

    public string FontFamily { get; set; }
        = "\"Segoe UI\", Arial, sans-serif";
}