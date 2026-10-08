using Renderset.Client.Internal;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Renderset.Client
{
    /// <summary>
    /// Enlace compartido (bundle): uno o varios documentos detrás de una
    /// URL pública con caducidad, para mandársela a un cliente.
    ///
    ///     var link = await renderset
    ///         .Share("Envío 3129 - Geimex")
    ///         .WithDocument(packingList.DocumentId, "Packing list")
    ///         .WithDocument(renderset.Report("factura").FromReader(r), "Factura")
    ///         .InCulture("en-GB")
    ///         .ExpiresInDays(30)
    ///         .CreateAsync();
    ///
    ///     // link.Url → https://…/share/…
    ///
    /// Los documentos pueden estar ya emitidos (por id) o emitirse en la
    /// misma llamada (pasando la petición).
    /// </summary>
    public sealed class ShareBuilder
    {
        private readonly RendersetClient _client;
        private readonly string _title;
        private readonly List<string> _items = new List<string>();

        private string _message;
        private string _culture;
        private int? _expiresInDays;
        private DateTime? _expiresAtUtc;
        private bool _allowDataDownload;

        internal ShareBuilder(
            RendersetClient client,
            string title)
        {
            _client = client;
            _title = title;
        }

        /// <summary>
        /// Un documento ya emitido. <paramref name="displayName"/> es lo que
        /// ve el cliente en la lista; sin él, el nombre del fichero.
        /// </summary>
        public ShareBuilder WithDocument(
            string documentId,
            string displayName = null)
        {
            if (string.IsNullOrWhiteSpace(documentId))
                throw new ArgumentException("documentId es obligatorio.", nameof(documentId));

            var item = new StringBuilder("{\"documentId\":");
            JsonWriter.WriteString(item, documentId);
            AppendDisplayName(item, displayName);
            item.Append('}');

            _items.Add(item.ToString());

            return this;
        }

        /// <summary>
        /// Un documento que se emite en la misma llamada.
        /// </summary>
        public ShareBuilder WithDocument(
            RenderBuilder render,
            string displayName = null)
        {
            if (render == null)
                throw new ArgumentNullException(nameof(render));

            var item = new StringBuilder("{\"render\":");
            item.Append(render.BuildRequestJson());
            AppendDisplayName(item, displayName);
            item.Append('}');

            _items.Add(item.ToString());

            return this;
        }

        /// <summary>Texto encima de los documentos. Sin él, el del tenant.</summary>
        public ShareBuilder WithMessage(string message)
        {
            _message = message;
            return this;
        }

        /// <summary>
        /// Idioma de la página (botones y avisos). Sin él, el del primer
        /// documento. Cada documento sale en el idioma con el que se emitió.
        /// </summary>
        public ShareBuilder InCulture(string culture)
        {
            _culture = culture;
            return this;
        }

        /// <summary>Caducidad en días. Sin ella, la del tenant (30 por defecto).</summary>
        public ShareBuilder ExpiresInDays(int days)
        {
            _expiresInDays = days;
            _expiresAtUtc = null;
            return this;
        }

        public ShareBuilder ExpiresAt(DateTime utc)
        {
            _expiresAtUtc = utc.ToUniversalTime();
            _expiresInDays = null;
            return this;
        }

        /// <summary>
        /// Deja al cliente exportar las tablas a CSV y bajarse el JSON (de los
        /// documentos emitidos con <see cref="RenderBuilder.IncludeData"/>).
        /// Sin esto, los datos se quitan de lo que se le sirve.
        /// </summary>
        public ShareBuilder AllowDataDownload(bool allow = true)
        {
            _allowDataDownload = allow;
            return this;
        }

        public async Task<SharedLink> CreateAsync(
            CancellationToken cancellationToken = default)
        {
            if (_items.Count == 0)
            {
                throw new InvalidOperationException(
                    "El enlace no tiene documentos. Añádelos con WithDocument.");
            }

            var dto = await _client.Transport.SendJsonAsync<BundleDto>(
                HttpMethod.Post,
                _client.Path("api/bundles/{tenantId}"),
                BuildJson(),
                cancellationToken).ConfigureAwait(false);

            return new SharedLink(dto);
        }

        public SharedLink Create()
        {
            return Task.Run(() => CreateAsync())
                .GetAwaiter()
                .GetResult();
        }

        internal string BuildJson()
        {
            var builder = new StringBuilder("{\"title\":");
            JsonWriter.WriteString(builder, _title);

            if (!string.IsNullOrEmpty(_message))
            {
                builder.Append(",\"message\":");
                JsonWriter.WriteString(builder, _message);
            }

            if (!string.IsNullOrEmpty(_culture))
            {
                builder.Append(",\"culture\":");
                JsonWriter.WriteString(builder, _culture);
            }

            if (_expiresInDays.HasValue)
            {
                builder.Append(",\"expiresInDays\":");
                builder.Append(_expiresInDays.Value.ToString(CultureInfo.InvariantCulture));
            }

            if (_expiresAtUtc.HasValue)
            {
                builder.Append(",\"expiresAtUtc\":");
                JsonWriter.WriteString(
                    builder,
                    _expiresAtUtc.Value.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
            }

            if (_allowDataDownload)
                builder.Append(",\"allowDataDownload\":true");

            builder.Append(",\"items\":[");
            builder.Append(string.Join(",", _items));
            builder.Append("]}");

            return builder.ToString();
        }

        private static void AppendDisplayName(
            StringBuilder builder,
            string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
                return;

            builder.Append(",\"displayName\":");
            JsonWriter.WriteString(builder, displayName);
        }
    }
}
