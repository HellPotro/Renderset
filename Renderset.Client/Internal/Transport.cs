using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Renderset.Client.Internal
{
    /// <summary>
    /// Envío con reintentos y lectura de respuestas.
    ///
    /// Para leer JSON se usa DataContractJsonSerializer, que viene con el
    /// propio framework (también en .NET Framework 4.6.1): el paquete sigue
    /// sin dependencias. Las fechas se leen como texto ISO y se convierten a
    /// mano, porque ese serializador sólo entiende el formato antiguo
    /// "\/Date(...)\/".
    /// </summary>
    internal sealed class Transport
    {
        private readonly HttpClient _http;
        private readonly RendersetClientOptions _options;

        public Transport(
            HttpClient http,
            RendersetClientOptions options)
        {
            _http = http;
            _options = options;
        }

        public string TenantPath(
            string tenantId,
            string path)
        {
            return path.Replace("{tenantId}", Uri.EscapeDataString(tenantId));
        }

        /// <summary>
        /// Envía y devuelve la respuesta si es 2xx; si no, lanza
        /// <see cref="RendersetException"/> con los errores del servidor.
        ///
        /// Reintentos, con espera creciente:
        /// <list type="bullet">
        /// <item>GET (descargar, consultar): cortes de red, timeouts, 5xx y 429.</item>
        /// <item>POST (emitir, compartir): sólo 429 y fallos de conexión. Un
        /// timeout o un 5xx pueden llegar con el documento ya emitido (p. ej.
        /// 502 pdf.generation_failed) y repetir la petición lo duplicaría.</item>
        /// </list>
        /// Un 4xx nunca: si la petición está mal, repetirla no la arregla.
        /// </summary>
        public async Task<HttpResponseMessage> SendAsync(
            HttpMethod method,
            string path,
            string jsonBody,
            CancellationToken cancellationToken)
        {
            var attempt = 0;
            var safeToRepeat = method == HttpMethod.Get;

            while (true)
            {
                attempt++;

                HttpResponseMessage response = null;

                try
                {
                    using (var request = new HttpRequestMessage(method, path))
                    {
                        if (jsonBody != null)
                        {
                            request.Content = new StringContent(
                                jsonBody,
                                Encoding.UTF8,
                                "application/json");
                        }

                        response = await _http
                            .SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken)
                            .ConfigureAwait(false);
                    }

                    if (response.IsSuccessStatusCode)
                    {
                        var success = response;
                        response = null;
                        return success;
                    }

                    var status = (int)response.StatusCode;

                    var content = await response.Content
                        .ReadAsStringAsync()
                        .ConfigureAwait(false);

                    var retry =
                        status == 429 ||
                        (status >= 500 && safeToRepeat);

                    if (!retry || attempt > _options.MaxRetries)
                        throw RendersetException.FromResponse(status, content);
                }
                catch (HttpRequestException) when (attempt <= _options.MaxRetries)
                {
                    // Corte de red: se reintenta.
                }
                catch (TaskCanceledException) when (
                    safeToRepeat &&
                    !cancellationToken.IsCancellationRequested &&
                    attempt <= _options.MaxRetries)
                {
                    // Timeout del HttpClient, no cancelación del llamante.
                }
                finally
                {
                    response?.Dispose();
                }

                await Task.Delay(
                    TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                    cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task<T> SendJsonAsync<T>(
            HttpMethod method,
            string path,
            string jsonBody,
            CancellationToken cancellationToken)
            where T : class
        {
            using (var response = await SendAsync(method, path, jsonBody, cancellationToken).ConfigureAwait(false))
            {
                var bytes = await response.Content
                    .ReadAsByteArrayAsync()
                    .ConfigureAwait(false);

                return Json.Read<T>(bytes);
            }
        }

        public async Task<RenderResult> SendFileAsync(
            HttpMethod method,
            string path,
            string jsonBody,
            CancellationToken cancellationToken)
        {
            using (var response = await SendAsync(method, path, jsonBody, cancellationToken).ConfigureAwait(false))
            {
                var content = await response.Content
                    .ReadAsByteArrayAsync()
                    .ConfigureAwait(false);

                var fileName = "document";

                var disposition = response.Content.Headers.ContentDisposition;

                if (disposition != null)
                {
                    fileName =
                        disposition.FileNameStar
                        ?? disposition.FileName
                        ?? fileName;

                    fileName = fileName.Trim('"');
                }

                var contentType =
                    response.Content.Headers.ContentType != null
                        ? response.Content.Headers.ContentType.MediaType
                        : "application/octet-stream";

                IEnumerable<string> values;

                var documentId =
                    response.Headers.TryGetValues("X-Renderset-Document-Id", out values)
                        ? string.Join(",", values)
                        : null;

                var location =
                    response.Headers.Location != null
                        ? response.Headers.Location.ToString()
                        : null;

                return new RenderResult(content, contentType, fileName, documentId, location);
            }
        }
    }


    /// <summary>
    /// Lectura de JSON con el serializador del framework.
    /// </summary>
    internal static class Json
    {
        public static T Read<T>(
            byte[] bytes)
            where T : class
        {
            if (bytes == null || bytes.Length == 0)
                return null;

            var serializer = new DataContractJsonSerializer(
                typeof(T),
                new DataContractJsonSerializerSettings
                {
                    UseSimpleDictionaryFormat = true
                });

            using (var stream = new MemoryStream(bytes))
            {
                return (T)serializer.ReadObject(stream);
            }
        }

        public static T Read<T>(
            string text)
            where T : class
        {
            return string.IsNullOrEmpty(text)
                ? null
                : Read<T>(Encoding.UTF8.GetBytes(text));
        }

        public static DateTime? Date(
            string value)
        {
            if (string.IsNullOrEmpty(value))
                return null;

            DateTime parsed;

            return DateTime.TryParse(
                value,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AdjustToUniversal |
                System.Globalization.DateTimeStyles.AssumeUniversal,
                out parsed)
                ? parsed
                : (DateTime?)null;
        }
    }


    // -------------------------------------------------------------- DTOs

    [DataContract]
    internal sealed class ErrorDto
    {
        [DataMember(Name = "code")] public string Code;
        [DataMember(Name = "message")] public string Message;
        [DataMember(Name = "path")] public string Path;
        [DataMember(Name = "expected")] public string Expected;
        [DataMember(Name = "actual")] public string Actual;
    }

    [DataContract]
    internal sealed class DocumentDto
    {
        [DataMember(Name = "documentId")] public string DocumentId;
        [DataMember(Name = "url")] public string Url;
        [DataMember(Name = "pdfUrl")] public string PdfUrl;
        [DataMember(Name = "fileName")] public string FileName;
        [DataMember(Name = "reportId")] public string ReportId;
        [DataMember(Name = "presetId")] public string PresetId;
        [DataMember(Name = "presetVersion")] public int PresetVersion;
        [DataMember(Name = "culture")] public string Culture;
        [DataMember(Name = "format")] public string Format;
        [DataMember(Name = "createdAtUtc")] public string CreatedAtUtc;
    }

    [DataContract]
    internal sealed class ValidationDto
    {
        [DataMember(Name = "valid")] public bool Valid;
        [DataMember(Name = "reportId")] public string ReportId;
        [DataMember(Name = "presetId")] public string PresetId;
        [DataMember(Name = "presetVersion")] public int? PresetVersion;
        [DataMember(Name = "errors")] public ErrorDto[] Errors;
    }

    [DataContract]
    internal sealed class BundleDto
    {
        [DataMember(Name = "bundleId")] public string BundleId;
        [DataMember(Name = "url")] public string Url;
        [DataMember(Name = "title")] public string Title;
        [DataMember(Name = "culture")] public string Culture;
        [DataMember(Name = "status")] public string Status;
        [DataMember(Name = "allowDataDownload")] public bool AllowDataDownload;
        [DataMember(Name = "createdAtUtc")] public string CreatedAtUtc;
        [DataMember(Name = "expiresAtUtc")] public string ExpiresAtUtc;
        [DataMember(Name = "accessCount")] public int AccessCount;
        [DataMember(Name = "documents")] public BundleDocumentDto[] Documents;
    }

    [DataContract]
    internal sealed class BundleDocumentDto
    {
        [DataMember(Name = "position")] public int Position;
        [DataMember(Name = "documentId")] public string DocumentId;
        [DataMember(Name = "displayName")] public string DisplayName;
    }

    [DataContract]
    internal sealed class DocumentPageDto
    {
        [DataMember(Name = "items")] public DocumentSummaryDto[] Items;
        [DataMember(Name = "nextCursor")] public string NextCursor;
    }

    [DataContract]
    internal sealed class DocumentSummaryDto
    {
        [DataMember(Name = "id")] public string Id;
        [DataMember(Name = "reportId")] public string ReportId;
        [DataMember(Name = "fileName")] public string FileName;
        [DataMember(Name = "culture")] public string Culture;
        [DataMember(Name = "createdAtUtc")] public string CreatedAtUtc;
        [DataMember(Name = "hasPdf")] public bool HasPdf;
    }
}
