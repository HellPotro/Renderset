using Renderset.Client.Internal;
using System;
using System.Data;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Renderset.Client
{
    /// <summary>
    /// Punto de entrada del cliente.
    ///
    ///     var pdf = await RendersetClient
    ///         .Create("https://api.renderset.io", apiKey)
    ///         .Report("invoice")
    ///         .WithMapping("invoice-erp-sql")
    ///         .FromReader(reader)
    ///         .GenerateAsync();
    ///
    ///     pdf.Save(@"C:\salida\10001.pdf");
    /// </summary>
    public sealed class RendersetClient
        : IDisposable
    {
        private readonly HttpClient _http;
        private readonly bool _ownsHttpClient;

        private RendersetClient(
            HttpClient http,
            bool ownsHttpClient,
            RendersetClientOptions options)
        {
            _http = http;
            _ownsHttpClient = ownsHttpClient;

            Options = options ?? new RendersetClientOptions();
        }

        internal RendersetClientOptions Options { get; }

        /// <summary>
        /// Crea un cliente con su propio HttpClient. Cómodo para una
        /// aplicación de escritorio; en un servicio, usa la sobrecarga que
        /// recibe un HttpClient para no agotar sockets.
        /// </summary>
        public static RendersetClient Create(
            string baseUrl,
            string apiKey,
            RendersetClientOptions options = null)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
                throw new ArgumentException("baseUrl es obligatorio.", nameof(baseUrl));

            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("apiKey es obligatorio.", nameof(apiKey));

            EnableModernTls();

            var settings = options ?? new RendersetClientOptions();

            var http = new HttpClient
            {
                BaseAddress = new Uri(
                    baseUrl.EndsWith("/") ? baseUrl : baseUrl + "/"),

                Timeout = settings.Timeout
            };

            http.DefaultRequestHeaders.Add("X-Api-Key", apiKey);

            return new RendersetClient(http, true, settings);
        }

        public static RendersetClient Create(
            HttpClient http,
            RendersetClientOptions options = null)
        {
            if (http == null)
                throw new ArgumentNullException(nameof(http));

            EnableModernTls();

            return new RendersetClient(http, false, options);
        }

        public RenderBuilder Report(
            string reportId)
        {
            if (string.IsNullOrWhiteSpace(reportId))
                throw new ArgumentException("reportId es obligatorio.", nameof(reportId));

            return new RenderBuilder(this, _http, reportId);
        }

        public void Dispose()
        {
            if (_ownsHttpClient)
                _http.Dispose();
        }

        /// <summary>
        /// .NET Framework 4.5 y 4.6 no negocian TLS 1.2 por defecto. Sin
        /// esto, el cliente ve "Se ha cerrado la conexión subyacente", que no
        /// dice absolutamente nada, y acabas tú depurándolo por teléfono.
        /// </summary>
        private static void EnableModernTls()
        {
#if NETSTANDARD2_0
            try
            {
                ServicePointManager.SecurityProtocol |=
                    SecurityProtocolType.Tls12;
            }
            catch (NotSupportedException)
            {
                // Runtime demasiado antiguo para conocer el valor. No hay
                // nada que hacer aquí y no merece tirar la aplicación.
            }
#endif
        }
    }


    public sealed class RendersetClientOptions
    {
        public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Reintentos ante fallo de red o error 5xx, con espera creciente.
        /// Los 4xx no se reintentan: si la petición está mal, repetirla la
        /// deja igual de mal.
        /// </summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// Corta antes de subir una barbaridad. Cero significa sin límite.
        /// </summary>
        public int MaxRows { get; set; } = 100_000;
    }


    public sealed class RenderBuilder
    {
        private readonly RendersetClient _client;
        private readonly HttpClient _http;
        private readonly string _reportId;

        private TabularPayload _rows;
        private string _rawJsonData;
        private string _mappingId;
        private string _presetId;
        private int? _presetVersion;
        private string _culture;
        private string _currency;
        private string _timeZone;
        private string _contextType;
        private string _contextKey;
        private string _fileName;
        private string _format = "pdf";
        private string _batchMode;
        private string _idempotencyKey;

        internal RenderBuilder(
            RendersetClient client,
            HttpClient http,
            string reportId)
        {
            _client = client;
            _http = http;
            _reportId = reportId;
        }

        // ---------------------------------------------------------- orígenes

        /// <summary>
        /// Envía el resultado de la consulta tal cual. Las columnas de
        /// cabecera repetidas por el JOIN se envían repetidas: agruparlas es
        /// trabajo del servidor, no tuyo.
        ///
        /// Consume el reader entero.
        /// </summary>
        public RenderBuilder FromReader(
            IDataReader reader)
        {
            _rows = TabularPayload.FromReader(
                reader,
                _client.Options.MaxRows);

            return this;
        }

        public RenderBuilder FromDataTable(
            DataTable table)
        {
            if (table == null)
                throw new ArgumentNullException(nameof(table));

            using (var reader = table.CreateDataReader())
            {
                return FromReader(reader);
            }
        }

        /// <summary>
        /// Ejecuta el comando y envía el resultado. Evita tener que escribir
        /// el using del reader.
        /// </summary>
        public RenderBuilder FromCommand(
            IDbCommand command)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));

            if (command.Connection.State != ConnectionState.Open)
                command.Connection.Open();

            using (var reader = command.ExecuteReader())
            {
                return FromReader(reader);
            }
        }

        /// <summary>
        /// Para quien ya tenga el modelo jerárquico montado. El JSON se envía
        /// tal cual, sin tocarlo.
        /// </summary>
        public RenderBuilder FromJson(
            string json)
        {
            _rawJsonData = json;

            return this;
        }

        // ---------------------------------------------------------- opciones

        public RenderBuilder WithMapping(string mappingId)
        {
            _mappingId = mappingId;
            return this;
        }

        public RenderBuilder WithPreset(string presetId, int? version = null)
        {
            _presetId = presetId;
            _presetVersion = version;
            return this;
        }

        public RenderBuilder WithCulture(string culture)
        {
            _culture = culture;
            return this;
        }

        public RenderBuilder WithCurrency(string currency)
        {
            _currency = currency;
            return this;
        }

        public RenderBuilder WithTimeZone(string timeZone)
        {
            _timeZone = timeZone;
            return this;
        }

        /// <summary>
        /// Deja que el servidor elija el diseño asignado a ese cliente.
        /// </summary>
        public RenderBuilder ForCustomer(string customerId)
        {
            _contextType = "customer";
            _contextKey = customerId;
            return this;
        }

        public RenderBuilder ForContext(string contextType, string contextKey)
        {
            _contextType = contextType;
            _contextKey = contextKey;
            return this;
        }

        public RenderBuilder WithFileName(string fileName)
        {
            _fileName = fileName;
            return this;
        }

        public RenderBuilder AsHtml()
        {
            _format = "html";
            return this;
        }

        /// <summary>
        /// Varios documentos en un solo PDF.
        /// </summary>
        public RenderBuilder AsMergedBatch()
        {
            _batchMode = "merged";
            return this;
        }

        /// <summary>
        /// Un fichero por documento, devueltos en un ZIP.
        /// </summary>
        public RenderBuilder AsSeparateFiles()
        {
            _batchMode = "separate";
            return this;
        }

        /// <summary>
        /// Misma clave, mismo documento. Sin esto, un timeout de red en tu
        /// lado puede generar la factura dos veces.
        /// </summary>
        public RenderBuilder WithIdempotencyKey(string key)
        {
            _idempotencyKey = key;
            return this;
        }

        // --------------------------------------------------------- ejecución

        public Task<RenderResult> GenerateAsync(
            CancellationToken cancellationToken = default)
        {
            return SendAsync("api/render", cancellationToken);
        }

        /// <summary>
        /// Comprueba los datos contra el mapping sin generar nada. Devuelve
        /// el JSON de validación del servidor, que dice qué columna falta o
        /// qué tipo no cuadra.
        /// </summary>
        public async Task<string> ValidateAsync(
            CancellationToken cancellationToken = default)
        {
            var result = await SendAsync(
                "api/render/validate",
                cancellationToken).ConfigureAwait(false);

            return Encoding.UTF8.GetString(result.Content);
        }

        /// <summary>
        /// Versión síncrona para WinForms y servicios antiguos.
        ///
        /// Usa Task.Run a propósito: llamar a .Result directamente desde el
        /// hilo de UI de WinForms bloquea la aplicación para siempre, porque
        /// la continuación espera un contexto de sincronización que está
        /// ocupado esperándola a ella.
        /// </summary>
        public RenderResult Generate()
        {
            return Task.Run(() => GenerateAsync())
                .GetAwaiter()
                .GetResult();
        }

        public string Validate()
        {
            return Task.Run(() => ValidateAsync())
                .GetAwaiter()
                .GetResult();
        }

        // ------------------------------------------------------------ interno

        private async Task<RenderResult> SendAsync(
            string path,
            CancellationToken cancellationToken)
        {
            EnsureValidRequest();

            var body = BuildJson();

            var attempt = 0;

            while (true)
            {
                attempt++;

                HttpResponseMessage response = null;

                try
                {
                    using (var request = new HttpRequestMessage(
                               HttpMethod.Post,
                               path))
                    {
                        request.Content = new StringContent(
                            body,
                            Encoding.UTF8,
                            "application/json");

                        if (!string.IsNullOrEmpty(_idempotencyKey))
                        {
                            request.Headers.Add(
                                "Idempotency-Key",
                                _idempotencyKey);
                        }

                        response = await _http
                            .SendAsync(request, cancellationToken)
                            .ConfigureAwait(false);
                    }

                    if (response.IsSuccessStatusCode)
                        return await ReadResultAsync(response).ConfigureAwait(false);

                    var status = (int)response.StatusCode;

                    var content = await response.Content
                        .ReadAsStringAsync()
                        .ConfigureAwait(false);

                    // Sólo se reintentan los errores del servidor y el 429.
                    // Un 400 es un problema de la petición y reintentarlo
                    // sólo retrasa el diagnóstico.
                    if (status < 500 && status != 429)
                        throw new RendersetException(status, content);

                    if (attempt > _client.Options.MaxRetries)
                        throw new RendersetException(status, content);
                }
                catch (HttpRequestException) when (attempt <= _client.Options.MaxRetries)
                {
                    // Corte de red: se reintenta.
                }
                catch (TaskCanceledException) when (
                    !cancellationToken.IsCancellationRequested &&
                    attempt <= _client.Options.MaxRetries)
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

        private static async Task<RenderResult> ReadResultAsync(
            HttpResponseMessage response)
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

            return new RenderResult(content, contentType, fileName);
        }

        private void EnsureValidRequest()
        {
            if (_rows == null && string.IsNullOrEmpty(_rawJsonData))
            {
                throw new InvalidOperationException(
                    "No se han indicado datos. Usa FromReader, FromDataTable, " +
                    "FromCommand o FromJson.");
            }

            if (_rows != null && !string.IsNullOrEmpty(_rawJsonData))
            {
                throw new InvalidOperationException(
                    "Se han indicado datos planos y jerárquicos a la vez. " +
                    "Elige uno.");
            }

            if (_rows != null && string.IsNullOrEmpty(_mappingId))
            {
                throw new InvalidOperationException(
                    "Al enviar filas planas hace falta WithMapping: es lo que " +
                    "dice cómo agruparlas en documentos.");
            }

            if (_rows != null && _rows.RowCount == 0)
            {
                throw new InvalidOperationException(
                    "La consulta no ha devuelto ninguna fila.");
            }
        }

        private string BuildJson()
        {
            var builder = new StringBuilder(
                _rows != null
                    ? Math.Max(1024, _rows.RowCount * _rows.ColumnCount * 12)
                    : 1024);

            builder.Append("{\"reportId\":");
            JsonWriter.WriteString(builder, _reportId);

            AppendString(builder, "mappingId", _mappingId);
            AppendString(builder, "presetId", _presetId);

            if (_presetVersion.HasValue)
            {
                builder.Append(",\"presetVersion\":");
                builder.Append(_presetVersion.Value);
            }

            AppendString(builder, "idempotencyKey", _idempotencyKey);

            // context
            if (_culture != null || _currency != null || _timeZone != null ||
                _contextType != null)
            {
                builder.Append(",\"context\":{");

                var first = true;

                AppendObjectMember(builder, "culture", _culture, ref first);
                AppendObjectMember(builder, "currency", _currency, ref first);
                AppendObjectMember(builder, "timeZone", _timeZone, ref first);
                AppendObjectMember(builder, "contextType", _contextType, ref first);
                AppendObjectMember(builder, "contextKey", _contextKey, ref first);

                builder.Append('}');
            }

            // output
            builder.Append(",\"output\":{\"format\":");
            JsonWriter.WriteString(builder, _format);

            if (!string.IsNullOrEmpty(_fileName))
            {
                builder.Append(",\"fileName\":");
                JsonWriter.WriteString(builder, _fileName);
            }

            if (!string.IsNullOrEmpty(_batchMode))
            {
                builder.Append(",\"batchMode\":");
                JsonWriter.WriteString(builder, _batchMode);
            }

            builder.Append('}');

            // datos
            if (_rows != null)
            {
                builder.Append(",\"rows\":");
                _rows.WriteTo(builder);
            }
            else
            {
                builder.Append(",\"data\":");
                builder.Append(_rawJsonData);
            }

            builder.Append('}');

            return builder.ToString();
        }

        private static void AppendString(
            StringBuilder builder,
            string name,
            string value)
        {
            if (string.IsNullOrEmpty(value))
                return;

            builder.Append(',');
            JsonWriter.WriteString(builder, name);
            builder.Append(':');
            JsonWriter.WriteString(builder, value);
        }

        private static void AppendObjectMember(
            StringBuilder builder,
            string name,
            string value,
            ref bool first)
        {
            if (string.IsNullOrEmpty(value))
                return;

            if (!first)
                builder.Append(',');

            first = false;

            JsonWriter.WriteString(builder, name);
            builder.Append(':');
            JsonWriter.WriteString(builder, value);
        }
    }


    public sealed class RenderResult
    {
        internal RenderResult(
            byte[] content,
            string contentType,
            string fileName)
        {
            Content = content;
            ContentType = contentType;
            FileName = fileName;
        }

        public byte[] Content { get; }

        public string ContentType { get; }

        /// <summary>
        /// Nombre propuesto por el servidor, ya resuelto si la plantilla
        /// llevaba variables.
        /// </summary>
        public string FileName { get; }

        public void Save(
            string path)
        {
            var target = Directory.Exists(path)
                ? Path.Combine(path, FileName)
                : path;

            File.WriteAllBytes(target, Content);
        }

        public Stream OpenRead()
        {
            return new MemoryStream(Content, false);
        }
    }


    public sealed class RendersetException
        : Exception
    {
        public RendersetException(
            int statusCode,
            string body)
            : base(BuildMessage(statusCode, body))
        {
            StatusCode = statusCode;
            Body = body;
        }

        public int StatusCode { get; }

        /// <summary>
        /// Cuerpo de la respuesta. El servidor devuelve ahí el motivo real:
        /// qué columna falta, qué tipo no cuadra.
        /// </summary>
        public string Body { get; }

        private static string BuildMessage(
            int statusCode,
            string body)
        {
            if (statusCode == 0)
                return body;

            return string.IsNullOrEmpty(body)
                ? "RenderSet devolvió " + statusCode + "."
                : "RenderSet devolvió " + statusCode + ": " + body;
        }
    }
}
