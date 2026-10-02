using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Renderset.Core.Rendering;

namespace Renderset.Infrastructure.Rendering;

/// <summary>
/// Descarga las imágenes remotas del documento y las incrusta como data URI.
///
/// La URL del logo la escribe un usuario, así que descargarla desde el
/// servidor es una puerta a la red interna (SSRF) si no se limita. Por eso:
/// sólo http/https, sólo image/*, tamaño máximo, tiempo máximo, y la IP se
/// comprueba en el momento de conectar (no sólo al resolver el nombre, que
/// se podría cambiar entre la comprobación y la conexión).
///
/// Si una imagen no se puede descargar se deja la URL original y se avisa
/// en el log: el documento se emite igual, como hasta ahora.
/// </summary>
public sealed class HttpHtmlAssetInliner
    : IHtmlAssetInliner,
      IDisposable
{
    private readonly DocumentAssetsOptions _options;
    private readonly ILogger<HttpHtmlAssetInliner> _logger;
    private readonly HttpClient _http;

    public HttpHtmlAssetInliner(
        DocumentAssetsOptions options,
        ILogger<HttpHtmlAssetInliner> logger)
    {
        _options = options;
        _logger = logger;

        var handler =
            new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 3,
                ConnectCallback = ConnectAsync
            };

        _http =
            new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(Math.Max(1, options.TimeoutSeconds))
            };

        _http.DefaultRequestHeaders.UserAgent.ParseAdd("RenderSet/1.0 (+asset-inliner)");
    }

    public async Task<string> InlineAsync(
        string html,
        CancellationToken cancellationToken = default)
    {
        if (!_options.InlineImages || string.IsNullOrEmpty(html))
            return html;

        var urls = HtmlImageSources.FindRemote(html);

        if (urls.Count == 0)
            return html;

        var replacements =
            new Dictionary<string, string>(StringComparer.Ordinal);

        // Normalmente es una sola imagen (el logo) repetida; se descarga una
        // vez por URL distinta.
        foreach (var url in urls)
        {
            var dataUri =
                await TryDownloadAsync(
                    url,
                    cancellationToken);

            if (dataUri is not null)
                replacements[url] = dataUri;
        }

        return HtmlImageSources.Replace(html, replacements);
    }

    public void Dispose() =>
        _http.Dispose();

    private async Task<string?> TryDownloadAsync(
        string url,
        CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return null;
        }

        try
        {
            using var response =
                await _http.GetAsync(
                    uri,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "No se incrusta la imagen {Url}: respondió {Status}.",
                    url,
                    (int)response.StatusCode);

                return null;
            }

            var mediaType =
                response.Content.Headers.ContentType?.MediaType;

            if (mediaType is null ||
                !mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "No se incrusta {Url}: no es una imagen ({MediaType}).",
                    url,
                    mediaType ?? "sin tipo");

                return null;
            }

            if (response.Content.Headers.ContentLength > _options.MaxImageBytes)
            {
                _logger.LogWarning(
                    "No se incrusta {Url}: pesa más de {Max} bytes.",
                    url,
                    _options.MaxImageBytes);

                return null;
            }

            var bytes =
                await ReadLimitedAsync(
                    response.Content,
                    _options.MaxImageBytes,
                    cancellationToken);

            if (bytes is null)
            {
                _logger.LogWarning(
                    "No se incrusta {Url}: pesa más de {Max} bytes.",
                    url,
                    _options.MaxImageBytes);

                return null;
            }

            return $"data:{mediaType};base64,{Convert.ToBase64String(bytes)}";
        }
        catch (Exception ex) when (
            ex is HttpRequestException or TaskCanceledException or IOException &&
            !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                ex,
                "No se ha podido descargar la imagen {Url} para incrustarla.",
                url);

            return null;
        }
    }

    /// <summary>
    /// Lee como mucho <paramref name="max"/> bytes: Content-Length puede no
    /// venir o mentir.
    /// </summary>
    private static async Task<byte[]?> ReadLimitedAsync(
        HttpContent content,
        int max,
        CancellationToken cancellationToken)
    {
        await using var stream =
            await content.ReadAsStreamAsync(cancellationToken);

        using var buffer = new MemoryStream();
        var chunk = new byte[81920];

        int read;

        while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > max)
                return null;

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    /// <summary>
    /// Resuelve el nombre y conecta sólo a direcciones permitidas. Se
    /// ejecuta en cada conexión, también tras una redirección.
    /// </summary>
    private async ValueTask<Stream> ConnectAsync(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
    {
        var addresses =
            await Dns.GetHostAddressesAsync(
                context.DnsEndPoint.Host,
                cancellationToken);

        var allowed =
            addresses
                .Where(x => _options.AllowPrivateNetworks || IsPublic(x))
                .ToList();

        if (allowed.Count == 0)
        {
            throw new HttpRequestException(
                $"La dirección de '{context.DnsEndPoint.Host}' no está permitida " +
                "(red interna). Activa DocumentAssets:AllowPrivateNetworks si el " +
                "logo está en la intranet.");
        }

        var socket =
            new Socket(SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = true
            };

        try
        {
            await socket.ConnectAsync(
                allowed.ToArray(),
                context.DnsEndPoint.Port,
                cancellationToken);

            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    public static bool IsPublic(
        IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        if (IPAddress.IsLoopback(address) ||
            address.Equals(IPAddress.Any) ||
            address.Equals(IPAddress.IPv6Any) ||
            address.Equals(IPAddress.Broadcast))
        {
            return false;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return !(address.IsIPv6LinkLocal ||
                     address.IsIPv6SiteLocal ||
                     address.IsIPv6UniqueLocal ||
                     address.IsIPv6Multicast);
        }

        var b = address.GetAddressBytes();

        return b[0] switch
        {
            10 => false,                                  // 10.0.0.0/8
            127 => false,                                 // loopback
            0 => false,                                   // "esta red"
            169 when b[1] == 254 => false,                // link-local y metadatos de la nube
            172 when b[1] >= 16 && b[1] <= 31 => false,   // 172.16.0.0/12
            192 when b[1] == 168 => false,                // 192.168.0.0/16
            100 when b[1] >= 64 && b[1] <= 127 => false,  // CGNAT 100.64.0.0/10
            >= 224 => false,                              // multicast y reservadas
            _ => true
        };
    }
}
