using Renderset.Core.Blocks;
using Renderset.Core.Configurations;
using Renderset.Core.Definitions;

namespace Renderset.Core.Resolved;

public sealed class ResolvedReportBlock
{
    public required string Id { get; init; }

    public string? Name { get; init; }

    public ReportBlockType Type { get; init; }

    public ReportTextBlockConfiguration? Text { get; init; }

    /// <summary>
    /// Bloque de campos fijos ya traducido. Nulo en el resto de tipos.
    /// </summary>
    public ResolvedFieldsBlock? Fields { get; init; }
}

/// <summary>
/// Campos fijos de un bloque, ya traducidos y con las variables del tenant
/// sustituidas. Los {{data.ruta}} se resuelven al pintar, con los datos del
/// documento.
/// </summary>
public sealed class ResolvedFieldsBlock
{
    public string? Title { get; init; }

    public bool ShowTitle { get; init; } = true;

    /// <summary>
    /// Normalizado: List o Grid.
    /// </summary>
    public ReportSectionLayout Layout { get; init; } = ReportSectionLayout.List;

    public int Columns { get; init; } = 1;

    public List<ResolvedFixedField> Fields { get; init; } = [];
}

public sealed class ResolvedFixedField
{
    public required string Id { get; init; }

    public required string Label { get; init; }

    public required string Value { get; init; }
}
