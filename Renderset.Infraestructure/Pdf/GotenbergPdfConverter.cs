using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using Renderset.Core.Rendering.Pdf;

namespace Renderset.Infrastructure.Pdf;

/// <summary>
/// Conversión a PDF con Gotenberg (https://gotenberg.dev), que es Chromium
/// headless detrás de una API HTTP.
///
/// Va en un contenedor aparte y no dentro de la API porque Chromium no
/// arranca en el sandbox de App Service para Windows, y en Linux obliga a
/// meter el navegador y sus dependencias en el despliegue de la API.
///
/// Usa su propio HttpClient y no el de IHttpClientFactory a propósito: los
/// clientes de la factoría heredan la resiliencia estándar de
/// ServiceDefaults, que corta cada intento a los 10 segundos, y un documento
/// largo puede tardar más en convertirse.
/// </summary>
public sealed class GotenbergPdfConverter
    : IPdfConverter,
      IDisposable
{
    private const string FooterTemplate =
        """
        <!DOCTYPE html>
        <html>
        <head>
        <meta charset="utf-8" />
        <style>
            html, body {
                margin: 0;
                padding: 0;
                width: 100%;
                -webkit-print-color-adjust: exact;
            }
            .rs-page-footer {
                box-sizing: border-box;
                width: 100%;
                padding: 0 {{SIDE}}mm;
                color: #6D7B83;
                font-family: Arial, "Liberation Sans", sans-serif;
                font-size: 8px;
                text-align: right;
            }
        </style>
        </head>
        <body>
        <div class="rs-page-footer">
            <span class="pageNumber"></span> / <span class="totalPages"></span>
        </div>
        </body>
        </html>
        """;

    private readonly HttpClient _http;
    private readonly SemaphoreSlim _slots;

    public GotenbergPdfConverter(
        PdfOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.GotenbergUrl))
        {
            throw new InvalidOperationException(
                "Pdf:GotenbergUrl es obligatorio para usar Gotenberg.");
        }

        _http =
            new HttpClient(
                new SocketsHttpHandler
                {
                    // Renueva las conexiones para que un cambio de IP del
                    // contenedor (redespliegue) no deje sockets colgados.
                    PooledConnectionLifetime = TimeSpan.FromMinutes(5)
                })
            {
                BaseAddress = new Uri(options.GotenbergUrl.TrimEnd('/') + "/"),
                Timeout = TimeSpan.FromSeconds(Math.Max(10, options.TimeoutSeconds))
            };

        if (!string.IsNullOrWhiteSpace(options.Username))
        {
            var credentials =
                Convert.ToBase64String(
                    Encoding.UTF8.GetBytes(
                        $"{options.Username}:{options.Password}"));

            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", credentials);
        }

        _slots = new SemaphoreSlim(Math.Max(1, options.MaxConcurrency));
    }

    public bool IsAvailable => true;

    public async Task<byte[]> ConvertHtmlAsync(
        string html,
        PdfPageOptions page,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(html);
        ArgumentNullException.ThrowIfNull(page);

        using var form = new MultipartFormDataContent();

        // Gotenberg exige que el documento se llame index.html.
        AddFile(form, "index.html", html, "text/html");

        if (page.ShowPageNumbers)
        {
            AddFile(
                form,
                "footer.html",
                FooterTemplate.Replace(
                    "{{SIDE}}",
                    Number(page.MarginRightMm),
                    StringComparison.Ordinal),
                "text/html");
        }

        var (width, height) = page.PaperSizeMm;

        // En pulgadas: es la unidad que entienden todas las versiones de
        // Gotenberg sin sufijo.
        AddField(form, "paperWidth", Inches(width));
        AddField(form, "paperHeight", Inches(height));
        AddField(form, "marginTop", Inches(page.MarginTopMm));
        AddField(form, "marginBottom", Inches(page.MarginBottomMm));
        AddField(form, "marginLeft", Inches(page.MarginLeftMm));
        AddField(form, "marginRight", Inches(page.MarginRightMm));
        AddField(form, "printBackground", "true");
        AddField(form, "preferCssPageSize", "false");

        return await SendAsync(
            "forms/chromium/convert/html",
            form,
            cancellationToken);
    }

    public async Task<byte[]> MergeAsync(
        IReadOnlyList<byte[]> pdfs,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdfs);

        if (pdfs.Count == 0)
            throw new ArgumentException("No hay PDF que unir.", nameof(pdfs));

        if (pdfs.Count == 1)
            return pdfs[0];

        using var form = new MultipartFormDataContent();

        // Gotenberg une por orden alfabético del nombre de fichero: el
        // número con ceros a la izquierda conserva el orden del bundle.
        for (var i = 0; i < pdfs.Count; i++)
        {
            var content = new ByteArrayContent(pdfs[i]);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");

            form.Add(content, "files", $"{i + 1:000}.pdf");
        }

        return await SendAsync(
            "forms/pdfengines/merge",
            form,
            cancellationToken);
    }

    public void Dispose()
    {
        _http.Dispose();
        _slots.Dispose();
    }

    private async Task<byte[]> SendAsync(
        string path,
        HttpContent content,
        CancellationToken cancellationToken)
    {
        await _slots.WaitAsync(cancellationToken);

        try
        {
            using var response =
                await _http.PostAsync(
                    path,
                    content,
                    cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var body =
                    await response.Content.ReadAsStringAsync(cancellationToken);

                throw new PdfConversionException(
                    $"Gotenberg devolvió {(int)response.StatusCode} en {path}: " +
                    (body.Length > 500 ? body[..500] : body));
            }

            return await response.Content.ReadAsByteArrayAsync(cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new PdfConversionException(
                $"No se ha podido conectar con Gotenberg ({_http.BaseAddress}).",
                ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new PdfConversionException(
                $"Gotenberg no ha respondido en {_http.Timeout.TotalSeconds:0} segundos.",
                ex);
        }
        finally
        {
            _slots.Release();
        }
    }

    private static void AddFile(
        MultipartFormDataContent form,
        string fileName,
        string content,
        string mediaType)
    {
        var file = new StringContent(content, Encoding.UTF8, mediaType);

        form.Add(file, "files", fileName);
    }

    private static void AddField(
        MultipartFormDataContent form,
        string name,
        string value) =>
        form.Add(new StringContent(value), name);

    private static string Inches(
        double millimetres) =>
        (millimetres / 25.4).ToString("0.####", CultureInfo.InvariantCulture);

    private static string Number(
        double value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture);
}
