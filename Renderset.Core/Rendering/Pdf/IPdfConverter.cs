namespace Renderset.Core.Rendering.Pdf;

/// <summary>
/// Convierte HTML en PDF y une varios PDF en uno.
///
/// La implementación real llama a Gotenberg (Chromium en un contenedor): el
/// PDF sale del mismo motor que pinta el navegador, así que respeta el CSS
/// del documento. Librerías que "dibujan" PDF sin navegador no lo hacen.
/// </summary>
public interface IPdfConverter
{
    /// <summary>
    /// Falso cuando no hay conversor configurado (Pdf:GotenbergUrl vacío).
    /// Las pantallas lo usan para ofrecer HTML en lugar de PDF.
    /// </summary>
    bool IsAvailable { get; }

    Task<byte[]> ConvertHtmlAsync(
        string html,
        PdfPageOptions page,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Une los PDF en el orden recibido.
    /// </summary>
    Task<byte[]> MergeAsync(
        IReadOnlyList<byte[]> pdfs,
        CancellationToken cancellationToken = default);
}


/// <summary>
/// Formato de página. Los márgenes van aquí y no en el CSS porque el pie
/// con la numeración vive dentro del margen inferior.
/// </summary>
public sealed class PdfPageOptions
{
    /// <summary>A4 o Letter.</summary>
    public string Paper { get; set; } = "A4";

    public double MarginTopMm { get; set; } = 14;

    public double MarginBottomMm { get; set; } = 16;

    public double MarginLeftMm { get; set; } = 14;

    public double MarginRightMm { get; set; } = 14;

    /// <summary>
    /// "1 / 3" en el pie de cada página.
    /// </summary>
    public bool ShowPageNumbers { get; set; } = true;

    public (double WidthMm, double HeightMm) PaperSizeMm =>
        Paper.Equals("Letter", StringComparison.OrdinalIgnoreCase)
            ? (215.9, 279.4)
            : (210, 297);
}


public sealed class PdfConversionException
    : Exception
{
    public PdfConversionException(
        string message,
        Exception? inner = null)
        : base(message, inner)
    {
    }
}


/// <summary>
/// Conversor de reserva cuando no hay Gotenberg configurado: la API arranca
/// igual y todo lo que no es PDF sigue funcionando.
/// </summary>
public sealed class UnavailablePdfConverter
    : IPdfConverter
{
    public bool IsAvailable => false;

    public Task<byte[]> ConvertHtmlAsync(
        string html,
        PdfPageOptions page,
        CancellationToken cancellationToken = default) =>
        throw new PdfConversionException(
            "No hay conversor de PDF configurado. Indica Pdf:GotenbergUrl en la configuración de la API.");

    public Task<byte[]> MergeAsync(
        IReadOnlyList<byte[]> pdfs,
        CancellationToken cancellationToken = default) =>
        throw new PdfConversionException(
            "No hay conversor de PDF configurado. Indica Pdf:GotenbergUrl en la configuración de la API.");
}
