namespace Renderset.Web.Components.Shared;

/// <summary>
/// Elemento del menú lateral de selección (<see cref="SideNav"/>).
/// </summary>
/// <param name="Key">Lo que devuelve al elegirlo. Único en el menú.</param>
/// <param name="Label">Texto principal; es por lo que se filtra.</param>
/// <param name="Hint">Segunda línea (p. ej. "3 presets"). También se filtra por ella.</param>
/// <param name="Icon">Clase de Font Awesome sin el prefijo de estilo: "fa-file-lines".</param>
/// <param name="Group">Título del grupo en el que aparece.</param>
/// <param name="Accent">Icono resaltado (elementos comunes).</param>
public sealed record SideNavItem(
    string Key,
    string Label,
    string? Hint,
    string Icon,
    string Group,
    bool Accent = false);
