using Renderset.Client.Internal;
using System;
using System.Net;
using System.Net.Http;

namespace Renderset.Client
{
    /// <summary>
    /// Punto de entrada del cliente de RenderSet.
    ///
    ///     var renderset = RendersetClient.Create(
    ///         "https://api.renderset.oran.local",
    ///         "rs_live_…",
    ///         "oran");
    ///
    ///     var pdf = await renderset
    ///         .Report("packing-list")
    ///         .FromReader(reader)          // el SELECT tal cual
    ///         .WithCulture("en-GB")
    ///         .GenerateAsync();
    ///
    ///     pdf.Save(@"C:\salida");
    ///
    /// Un cliente por aplicación: es seguro usarlo desde varios hilos.
    /// </summary>
    public sealed class RendersetClient
        : IDisposable
    {
        private readonly HttpClient _http;
        private readonly bool _ownsHttpClient;

        private RendersetClient(
            HttpClient http,
            bool ownsHttpClient,
            string tenantId,
            RendersetClientOptions options)
        {
            _http = http;
            _ownsHttpClient = ownsHttpClient;

            TenantId = tenantId;
            Options = options ?? new RendersetClientOptions();
            Transport = new Transport(http, Options);
            Documents = new DocumentsClient(this);
        }

        /// <summary>
        /// Tenant de la clave. Va en la ruta de cada llamada; con una clave
        /// de tenant tiene que ser el suyo o la API responde 403.
        /// </summary>
        public string TenantId { get; }

        internal RendersetClientOptions Options { get; }

        internal Transport Transport { get; }

        /// <summary>
        /// Documentos ya emitidos: buscar, volver a descargar (PDF o HTML) y
        /// consultar sus datos.
        /// </summary>
        public DocumentsClient Documents { get; }

        /// <summary>
        /// Crea un cliente con su propio HttpClient. Cómodo para una
        /// aplicación de escritorio; en un servicio, usa la sobrecarga que
        /// recibe un HttpClient para no agotar sockets.
        /// </summary>
        /// <param name="baseUrl">URL de la API, p. ej. https://api.renderset.oran.local</param>
        /// <param name="apiKey">Clave del tenant (rs_live_…). Se crea en RenderSet → API.</param>
        /// <param name="tenantId">Tenant de la clave.</param>
        public static RendersetClient Create(
            string baseUrl,
            string apiKey,
            string tenantId,
            RendersetClientOptions options = null)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
                throw new ArgumentException("baseUrl es obligatorio.", nameof(baseUrl));

            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("apiKey es obligatorio.", nameof(apiKey));

            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("tenantId es obligatorio.", nameof(tenantId));

            EnableModernTls();

            var settings = options ?? new RendersetClientOptions();

            var http = new HttpClient
            {
                BaseAddress = new Uri(
                    baseUrl.EndsWith("/") ? baseUrl : baseUrl + "/"),

                Timeout = settings.Timeout
            };

            http.DefaultRequestHeaders.Add("X-Api-Key", apiKey);

            return new RendersetClient(http, true, tenantId, settings);
        }

        /// <summary>
        /// Con un HttpClient de la aplicación (IHttpClientFactory, por
        /// ejemplo). Tiene que traer ya BaseAddress y la cabecera X-Api-Key.
        /// </summary>
        public static RendersetClient Create(
            HttpClient http,
            string tenantId,
            RendersetClientOptions options = null)
        {
            if (http == null)
                throw new ArgumentNullException(nameof(http));

            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("tenantId es obligatorio.", nameof(tenantId));

            EnableModernTls();

            return new RendersetClient(http, false, tenantId, options);
        }

        /// <summary>
        /// Empieza una petición de documento para un report.
        /// </summary>
        public RenderBuilder Report(
            string reportId)
        {
            if (string.IsNullOrWhiteSpace(reportId))
                throw new ArgumentException("reportId es obligatorio.", nameof(reportId));

            return new RenderBuilder(this, reportId);
        }

        /// <summary>
        /// Empieza un enlace compartido (bundle): varios documentos detrás
        /// de una URL pública con caducidad.
        /// </summary>
        public ShareBuilder Share(
            string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("title es obligatorio.", nameof(title));

            return new ShareBuilder(this, title);
        }

        internal string Path(
            string template)
        {
            return Transport.TenantPath(TenantId, template);
        }

        public void Dispose()
        {
            if (_ownsHttpClient)
                _http.Dispose();
        }

        /// <summary>
        /// .NET Framework 4.5 y 4.6 no negocian TLS 1.2 por defecto. Sin
        /// esto, el cliente ve "Se ha cerrado la conexión subyacente", que no
        /// dice absolutamente nada.
        /// </summary>
        private static void EnableModernTls()
        {
            try
            {
                ServicePointManager.SecurityProtocol |=
                    SecurityProtocolType.Tls12;
            }
            catch (NotSupportedException)
            {
                // Runtime demasiado antiguo o plataforma que no lo permite
                // (PlatformNotSupportedException hereda de esta). No merece
                // tirar la aplicación.
            }
        }
    }


    public sealed class RendersetClientOptions
    {
        /// <summary>
        /// Tiempo máximo de cada llamada. Un PDF de muchas páginas puede
        /// tardar; cinco minutos por defecto.
        /// </summary>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Reintentos con espera creciente (2, 4, 8 s…). Las consultas y
        /// descargas se repiten ante fallo de red, timeout, 5xx o 429; las
        /// emisiones sólo ante 429 o fallo de conexión, para no duplicar
        /// documentos. Los 4xx no se reintentan nunca.
        /// </summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// Corta antes de subir una barbaridad. La API acepta hasta 50.000
        /// filas por petición. Cero significa sin límite en el cliente.
        /// </summary>
        public int MaxRows { get; set; } = 50_000;
    }
}
