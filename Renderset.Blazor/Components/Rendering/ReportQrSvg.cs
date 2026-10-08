using System.Globalization;
using System.Text;
using QRCoder;

namespace Renderset.Blazor.Rendering;

/// <summary>
/// QR como SVG en línea, para la cabecera del documento.
///
/// SVG y no imagen: el documento emitido tiene que ser autosuficiente (sin
/// peticiones a ningún servicio), se imprime nítido a cualquier tamaño y el
/// PDF lo conserva vectorial. Se dibuja a partir de la matriz de módulos de
/// QRCoder, sin depender de su generador de imágenes.
/// </summary>
public static class ReportQrSvg
{
    public static string? Render(
        string? text,
        int size)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        List<System.Collections.BitArray> matrix;

        try
        {
            using var generator = new QRCodeGenerator();

            // Nivel M: aguanta una mancha o un doblez sin hacer el código
            // mucho más denso que el L.
            using var data =
                generator.CreateQrCode(
                    text,
                    QRCodeGenerator.ECCLevel.M);

            matrix = data.ModuleMatrix;
        }
        catch (Exception)
        {
            // Un texto que no cabe en un QR no puede tirar el documento.
            return null;
        }

        var modules = matrix.Count;

        if (modules == 0)
            return null;

        // Un único path con un cuadrado por módulo oscuro: pesa poco y no
        // deja líneas finas entre módulos al escalar.
        var path = new StringBuilder();

        for (var y = 0; y < modules; y++)
        {
            var row = matrix[y];

            for (var x = 0; x < row.Length; x++)
            {
                if (!row[x])
                    continue;

                path.Append('M')
                    .Append(x.ToString(CultureInfo.InvariantCulture))
                    .Append(' ')
                    .Append(y.ToString(CultureInfo.InvariantCulture))
                    .Append("h1v1h-1z");
            }
        }

        var dimension = modules.ToString(CultureInfo.InvariantCulture);
        var pixels = size.ToString(CultureInfo.InvariantCulture);

        return
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {dimension} {dimension}\" " +
            $"width=\"{pixels}\" height=\"{pixels}\" shape-rendering=\"crispEdges\" role=\"img\" aria-label=\"QR\">" +
            $"<rect width=\"{dimension}\" height=\"{dimension}\" fill=\"#FFFFFF\"/>" +
            $"<path d=\"{path}\" fill=\"#000000\"/>" +
            "</svg>";
    }
}
