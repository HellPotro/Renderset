using Renderset.Client.Internal;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Renderset.Client
{
    /// <summary>
    /// Documentos ya emitidos. Son inmutables: lo que se descarga es lo que
    /// se emitió, aunque después haya cambiado el diseño.
    /// </summary>
    public sealed class DocumentsClient
    {
        private readonly RendersetClient _client;

        internal DocumentsClient(
            RendersetClient client)
        {
            _client = client;
        }

        /// <summary>Datos del documento (report, diseño y versión, idioma, URLs), sin el contenido.</summary>
        public async Task<RenderedDocument> GetAsync(
            string documentId,
            CancellationToken cancellationToken = default)
        {
            var dto = await _client.Transport.SendJsonAsync<DocumentDto>(
                HttpMethod.Get,
                DocumentPath(documentId, "/metadata"),
                null,
                cancellationToken).ConfigureAwait(false);

            return new RenderedDocument(dto);
        }

        /// <summary>
        /// El PDF. Se genera la primera vez que se pide y después se sirve el
        /// guardado.
        /// </summary>
        public Task<RenderResult> DownloadPdfAsync(
            string documentId,
            CancellationToken cancellationToken = default)
        {
            return _client.Transport.SendFileAsync(
                HttpMethod.Get,
                DocumentPath(documentId, "/pdf?download=true"),
                null,
                cancellationToken);
        }

        /// <summary>El HTML del documento.</summary>
        public Task<RenderResult> DownloadHtmlAsync(
            string documentId,
            CancellationToken cancellationToken = default)
        {
            return _client.Transport.SendFileAsync(
                HttpMethod.Get,
                DocumentPath(documentId, "/download"),
                null,
                cancellationToken);
        }

        /// <summary>
        /// Documentos emitidos, los más recientes primero. Para la página
        /// siguiente, otra vez con <c>cursor</c> = <see cref="DocumentPage.NextCursor"/>.
        /// </summary>
        public async Task<DocumentPage> SearchAsync(
            string search = null,
            string reportId = null,
            DateTime? fromUtc = null,
            DateTime? toUtc = null,
            int take = 50,
            string cursor = null,
            CancellationToken cancellationToken = default)
        {
            var query = new List<string>();

            void Add(string name, string value)
            {
                if (!string.IsNullOrEmpty(value))
                    query.Add(name + "=" + Uri.EscapeDataString(value));
            }

            Add("search", search);
            Add("reportId", reportId);
            Add("fromUtc", fromUtc?.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
            Add("toUtc", toUtc?.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
            Add("take", take.ToString(CultureInfo.InvariantCulture));
            Add("cursor", cursor);

            var path = _client.Path("api/documents/{tenantId}") +
                       (query.Count == 0 ? string.Empty : "?" + string.Join("&", query));

            var dto = await _client.Transport.SendJsonAsync<DocumentPageDto>(
                HttpMethod.Get,
                path,
                null,
                cancellationToken).ConfigureAwait(false);

            return new DocumentPage(dto ?? new DocumentPageDto());
        }

        public RenderResult DownloadPdf(string documentId)
        {
            return Task.Run(() => DownloadPdfAsync(documentId))
                .GetAwaiter()
                .GetResult();
        }

        private string DocumentPath(
            string documentId,
            string suffix)
        {
            if (string.IsNullOrWhiteSpace(documentId))
                throw new ArgumentException("documentId es obligatorio.", nameof(documentId));

            return _client.Path("api/documents/{tenantId}/") +
                   Uri.EscapeDataString(documentId) +
                   suffix;
        }
    }
}
