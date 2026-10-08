using Renderset.Deca.Validation;

namespace Renderset.Deca;

/// <summary>
/// Un DeCA tal como lo devuelve la API: el documento, su enlace público y si
/// el QR descarga ahora mismo.
/// </summary>
public sealed class DecaDetail
{
    public required DecaDocument Deca { get; init; }

    /// <summary>
    /// La URL que lleva el QR. Nula si falta el dominio público.
    /// </summary>
    public string? PublicUrl { get; init; }

    public bool IsPublicDownloadActive { get; init; }

    /// <summary>
    /// Avisos de la emisión (NIF extranjero, matrícula no española).
    /// </summary>
    public List<DecaIssue> Warnings { get; init; } = [];
}

public sealed class DecaValidationResponse
{
    public bool IsValid { get; init; }

    public List<DecaIssue> Issues { get; init; } = [];
}

public sealed class FinishDecaRequest
{
    /// <summary>
    /// Fin del servicio. Nulo = ahora.
    /// </summary>
    public DateTimeOffset? EndedAtUtc { get; init; }
}

/// <summary>
/// Diseño del DeCA para el diseñador del portal: siempre una configuración
/// completa para editar (la del tenant o, si no tiene, la base con su marca).
/// </summary>
public sealed class DecaTemplateResponse
{
    public required Renderset.Core.Configurations.ReportConfiguration Configuration { get; init; }

    public required Renderset.Core.Themes.ReportTheme Theme { get; init; }

    public Dictionary<string, string> Texts { get; init; } = [];

    /// <summary>
    /// Versión vigente (0 = el diseño base). Va de vuelta al guardar para
    /// no pisar lo que otro haya guardado entretanto.
    /// </summary>
    public int Version { get; init; }

    public bool IsBase { get; init; }

    public DateTime? UpdatedAtUtc { get; init; }

    public string? UpdatedBy { get; init; }
}

public sealed class SaveDecaTemplateRequest
{
    public required Renderset.Core.Configurations.ReportConfiguration Configuration { get; init; }

    public required Renderset.Core.Themes.ReportTheme Theme { get; init; }

    public Dictionary<string, string> Texts { get; init; } = [];

    public int ExpectedVersion { get; init; }
}
