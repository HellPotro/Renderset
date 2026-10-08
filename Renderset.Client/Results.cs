using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Renderset.Client.Internal;

namespace Renderset.Client
{
    /// <summary>
    /// Fichero generado (PDF o HTML) con los datos del documento emitido.
    /// </summary>
    public sealed class RenderResult
    {
        internal RenderResult(
            byte[] content,
            string contentType,
            string fileName,
            string documentId = null,
            string documentUrl = null)
        {
            Content = content;
            ContentType = contentType;
            FileName = fileName;
            DocumentId = documentId;
            DocumentUrl = documentUrl;
        }

        public byte[] Content { get; }

        /// <summary>application/pdf o text/html.</summary>
        public string ContentType { get; }

        /// <summary>
        /// Nombre propuesto por el servidor, ya resuelto si la plantilla
        /// llevaba variables.
        /// </summary>
        public string FileName { get; }

        /// <summary>
        /// Id del documento guardado en RenderSet. Sirve para volver a
        /// descargarlo o para compartirlo después. Nulo al descargar un
        /// documento que ya existía.
        /// </summary>
        public string DocumentId { get; }

        /// <summary>URL del documento en la API (requiere clave).</summary>
        public string DocumentUrl { get; }

        public bool IsPdf
        {
            get { return string.Equals(ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase); }
        }

        /// <summary>
        /// Guarda el fichero. Si <paramref name="path"/> es una carpeta, con
        /// el nombre que propone el servidor.
        /// </summary>
        public string Save(
            string path)
        {
            var target = Directory.Exists(path)
                ? Path.Combine(path, FileName)
                : path;

            File.WriteAllBytes(target, Content);

            return target;
        }

        public Stream OpenRead()
        {
            return new MemoryStream(Content, false);
        }
    }


    /// <summary>
    /// Documento emitido, sin el contenido: lo que devuelve
    /// <see cref="RenderBuilder.EmitAsync"/> y <see cref="DocumentsClient.GetAsync"/>.
    /// </summary>
    public sealed class RenderedDocument
    {
        internal RenderedDocument(DocumentDto dto)
        {
            DocumentId = dto.DocumentId;
            Url = dto.Url;
            PdfUrl = dto.PdfUrl;
            FileName = dto.FileName;
            ReportId = dto.ReportId;
            PresetId = dto.PresetId;
            PresetVersion = dto.PresetVersion;
            Culture = dto.Culture;
            Format = dto.Format;
            CreatedAtUtc = Json.Date(dto.CreatedAtUtc) ?? DateTime.MinValue;
        }

        public string DocumentId { get; }

        /// <summary>HTML del documento en la API (requiere clave).</summary>
        public string Url { get; }

        /// <summary>PDF del documento en la API. Nulo si la API no tiene conversor.</summary>
        public string PdfUrl { get; }

        public string FileName { get; }

        public string ReportId { get; }

        public string PresetId { get; }

        /// <summary>Versión del diseño con la que se emitió.</summary>
        public int PresetVersion { get; }

        public string Culture { get; }

        public string Format { get; }

        public DateTime CreatedAtUtc { get; }
    }


    /// <summary>Documento en un listado de <see cref="DocumentsClient.SearchAsync"/>.</summary>
    public sealed class DocumentSummary
    {
        internal DocumentSummary(DocumentSummaryDto dto)
        {
            DocumentId = dto.Id;
            ReportId = dto.ReportId;
            FileName = dto.FileName;
            Culture = dto.Culture;
            HasPdf = dto.HasPdf;
            CreatedAtUtc = Json.Date(dto.CreatedAtUtc) ?? DateTime.MinValue;
        }

        public string DocumentId { get; }
        public string ReportId { get; }
        public string FileName { get; }
        public string Culture { get; }
        public bool HasPdf { get; }
        public DateTime CreatedAtUtc { get; }
    }


    /// <summary>
    /// Una página de documentos. Para la siguiente, se repite la búsqueda
    /// con <see cref="NextCursor"/>; nulo es que no hay más.
    /// </summary>
    public sealed class DocumentPage
    {
        internal DocumentPage(DocumentPageDto dto)
        {
            Items = (dto.Items ?? new DocumentSummaryDto[0])
                .Select(x => new DocumentSummary(x))
                .ToList();

            NextCursor = dto.NextCursor;
        }

        public IReadOnlyList<DocumentSummary> Items { get; }

        public string NextCursor { get; }
    }


    /// <summary>
    /// Resultado de <see cref="RenderBuilder.ValidateAsync"/>: si la
    /// petición emitiría un documento y, si no, por qué.
    /// </summary>
    public sealed class RenderValidation
    {
        internal RenderValidation(ValidationDto dto)
        {
            Valid = dto.Valid;
            ReportId = dto.ReportId;
            PresetId = dto.PresetId;
            PresetVersion = dto.PresetVersion;
            Errors = (dto.Errors ?? new ErrorDto[0])
                .Select(x => new RendersetError(x))
                .ToList();
        }

        public bool Valid { get; }

        public string ReportId { get; }

        /// <summary>Diseño que se usaría con esos datos y ese contexto.</summary>
        public string PresetId { get; }

        public int? PresetVersion { get; }

        public IReadOnlyList<RendersetError> Errors { get; }

        public override string ToString()
        {
            return Valid
                ? "Válida (preset " + PresetId + " v" + PresetVersion + ")"
                : string.Join(Environment.NewLine, Errors.Select(x => x.ToString()));
        }
    }


    /// <summary>
    /// Un error de la API: código estable (para programar contra él),
    /// mensaje en español y dónde (columna, campo de la petición).
    /// </summary>
    public sealed class RendersetError
    {
        internal RendersetError(ErrorDto dto)
        {
            Code = dto.Code;
            Message = dto.Message;
            Path = dto.Path;
            Expected = dto.Expected;
            Actual = dto.Actual;
        }

        /// <summary>p. ej. rows.invalid, mapping.not_configured, preset.not_found.</summary>
        public string Code { get; }

        public string Message { get; }

        public string Path { get; }

        public string Expected { get; }

        public string Actual { get; }

        public override string ToString()
        {
            return string.IsNullOrEmpty(Path)
                ? Code + ": " + Message
                : Code + " (" + Path + "): " + Message;
        }
    }


    /// <summary>Enlace compartido creado con <see cref="ShareBuilder.CreateAsync"/>.</summary>
    public sealed class SharedLink
    {
        internal SharedLink(BundleDto dto)
        {
            BundleId = Guid.TryParse(dto.BundleId, out var id) ? id : Guid.Empty;
            Url = dto.Url;
            Title = dto.Title;
            Culture = dto.Culture;
            Status = dto.Status;
            AllowDataDownload = dto.AllowDataDownload;
            CreatedAtUtc = Json.Date(dto.CreatedAtUtc) ?? DateTime.MinValue;
            ExpiresAtUtc = Json.Date(dto.ExpiresAtUtc) ?? DateTime.MinValue;
            AccessCount = dto.AccessCount;
            DocumentIds = (dto.Documents ?? new BundleDocumentDto[0])
                .OrderBy(x => x.Position)
                .Select(x => x.DocumentId)
                .ToList();
        }

        public Guid BundleId { get; }

        /// <summary>
        /// Enlace público para el cliente. Nulo sólo en bundles antiguos cuyo
        /// enlace no se puede recuperar.
        /// </summary>
        public string Url { get; }

        public string Title { get; }

        public string Culture { get; }

        /// <summary>Active, Expired o Revoked.</summary>
        public string Status { get; }

        public bool AllowDataDownload { get; }

        public DateTime CreatedAtUtc { get; }

        public DateTime ExpiresAtUtc { get; }

        public int AccessCount { get; }

        public IReadOnlyList<string> DocumentIds { get; }
    }


    /// <summary>
    /// La API ha rechazado la petición. <see cref="Errors"/> trae el motivo
    /// real (qué columna falta, qué preset no existe) y
    /// <see cref="StatusCode"/> el código HTTP.
    /// </summary>
    public sealed class RendersetException
        : Exception
    {
        public RendersetException(
            int statusCode,
            string body)
            : this(statusCode, body, new List<RendersetError>())
        {
        }

        private RendersetException(
            int statusCode,
            string body,
            IReadOnlyList<RendersetError> errors)
            : base(BuildMessage(statusCode, body, errors))
        {
            StatusCode = statusCode;
            Body = body;
            Errors = errors;
        }

        /// <summary>0 cuando el error es del propio cliente (p. ej. demasiadas filas).</summary>
        public int StatusCode { get; }

        /// <summary>Cuerpo de la respuesta tal cual.</summary>
        public string Body { get; }

        /// <summary>Errores de la API ya leídos. Vacío si la respuesta no los traía.</summary>
        public IReadOnlyList<RendersetError> Errors { get; }

        internal static RendersetException FromResponse(
            int statusCode,
            string body)
        {
            IReadOnlyList<RendersetError> errors;

            try
            {
                var trimmed = (body ?? string.Empty).TrimStart();

                errors = trimmed.StartsWith("[", StringComparison.Ordinal)
                    ? (Json.Read<ErrorDto[]>(trimmed) ?? new ErrorDto[0])
                        .Select(x => new RendersetError(x))
                        .ToList()
                    : new List<RendersetError>();
            }
            catch (Exception)
            {
                // Un cuerpo que no es la lista de errores (un proxy, una
                // página de error): se queda en Body.
                errors = new List<RendersetError>();
            }

            return new RendersetException(statusCode, body, errors);
        }

        private static string BuildMessage(
            int statusCode,
            string body,
            IReadOnlyList<RendersetError> errors)
        {
            if (statusCode == 0)
                return body;

            if (errors != null && errors.Count > 0)
            {
                return "RenderSet devolvió " + statusCode + ": " +
                       string.Join(" ", errors.Select(x => x.Message));
            }

            return string.IsNullOrEmpty(body)
                ? "RenderSet devolvió " + statusCode + "."
                : "RenderSet devolvió " + statusCode + ": " + body;
        }
    }
}
