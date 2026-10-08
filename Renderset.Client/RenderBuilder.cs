using Renderset.Client.Internal;
using System;
using System.Data;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Renderset.Client
{
    /// <summary>
    /// Una petición de documento. Se construye encadenando:
    ///
    ///     renderset.Report("packing-list")
    ///         .FromReader(reader)       // o FromDataTable, FromCommand, FromJson
    ///         .ForCustomer("002297")    // opcional: diseño asignado a ese cliente
    ///         .WithCulture("en-GB")     // opcional: idioma y formatos
    ///         .IncludeData()            // opcional: CSV/JSON en el visor
    ///         .GenerateAsync();         // el PDF
    ///
    /// Y se termina con una de estas:
    /// <list type="bullet">
    /// <item><see cref="GenerateAsync"/>: emite y devuelve el fichero.</item>
    /// <item><see cref="EmitAsync"/>: emite y devuelve sólo los datos del documento (id, URLs), sin descargarlo.</item>
    /// <item><see cref="ValidateAsync"/>: comprueba sin emitir nada.</item>
    /// </list>
    /// </summary>
    public sealed class RenderBuilder
    {
        private readonly RendersetClient _client;
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
        private string _format = "Pdf";
        private bool _includeData;

        internal RenderBuilder(
            RendersetClient client,
            string reportId)
        {
            _client = client;
            _reportId = reportId;
        }

        // ---------------------------------------------------------- orígenes

        /// <summary>
        /// Envía el resultado de la consulta tal cual. Las columnas de
        /// cabecera repetidas por el JOIN se envían repetidas: agruparlas en
        /// documento, albaranes y líneas es trabajo del mapping del report.
        ///
        /// Consume el reader entero. Los tipos salen del proveedor, no de
        /// mirar los valores: una referencia "00123" llega como texto.
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
        /// Ejecuta el comando y envía el resultado. Abre la conexión si hace
        /// falta (y la deja como estaba).
        /// </summary>
        public RenderBuilder FromCommand(
            IDbCommand command)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));

            var opened = false;

            if (command.Connection.State != ConnectionState.Open)
            {
                command.Connection.Open();
                opened = true;
            }

            try
            {
                using (var reader = command.ExecuteReader())
                {
                    return FromReader(reader);
                }
            }
            finally
            {
                if (opened)
                    command.Connection.Close();
            }
        }

        /// <summary>
        /// El documento ya en JSON jerárquico (sin mapping). Se envía tal
        /// cual, sin tocarlo.
        /// </summary>
        public RenderBuilder FromJson(
            string json)
        {
            _rawJsonData = json;

            return this;
        }

        // ---------------------------------------------------------- opciones

        /// <summary>
        /// Mapping concreto para las filas. Sin esto se usa el del report,
        /// que es lo normal: sólo hace falta si el report tiene varios.
        /// </summary>
        public RenderBuilder WithMapping(string mappingId)
        {
            _mappingId = mappingId;
            return this;
        }

        /// <summary>
        /// Diseño concreto (y versión). Sin esto, el asignado al contexto o
        /// el de por defecto del report.
        /// </summary>
        public RenderBuilder WithPreset(string presetId, int? version = null)
        {
            _presetId = presetId;
            _presetVersion = version;
            return this;
        }

        /// <summary>Idioma de textos, números y fechas: es-ES, en-GB, fr-FR…</summary>
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
        /// Que el servidor use el diseño asignado a ese cliente (Asignaciones
        /// en RenderSet).
        /// </summary>
        public RenderBuilder ForCustomer(string customerId)
        {
            return ForContext("customer", customerId);
        }

        /// <summary>
        /// Igual que <see cref="ForCustomer"/> para otro tipo de contexto
        /// (proveedor, planta…).
        /// </summary>
        public RenderBuilder ForContext(string contextType, string contextKey)
        {
            _contextType = contextType;
            _contextKey = contextKey;
            return this;
        }

        /// <summary>
        /// Nombre del fichero. Admite datos del documento y variables de
        /// empresa: "salida-{{data.salidaid}}.html". En PDF la extensión se
        /// cambia sola a .pdf.
        /// </summary>
        public RenderBuilder WithFileName(string fileName)
        {
            _fileName = fileName;
            return this;
        }

        /// <summary>PDF (por defecto).</summary>
        public RenderBuilder AsPdf()
        {
            _format = "Pdf";
            return this;
        }

        /// <summary>HTML en vez de PDF: más rápido y no necesita el conversor.</summary>
        public RenderBuilder AsHtml()
        {
            _format = "Html";
            return this;
        }

        /// <summary>
        /// Guarda los datos dentro del documento, para que el visor de
        /// RenderSet (y un enlace compartido que lo permita) ofrezca exportar
        /// sus tablas a CSV y descargar el JSON. El documento pesa más.
        /// </summary>
        public RenderBuilder IncludeData(bool include = true)
        {
            _includeData = include;
            return this;
        }

        // --------------------------------------------------------- ejecución

        /// <summary>
        /// Emite el documento y devuelve el fichero (PDF o HTML) en la misma
        /// llamada. El documento queda guardado: su id está en
        /// <see cref="RenderResult.DocumentId"/>.
        /// </summary>
        public Task<RenderResult> GenerateAsync(
            CancellationToken cancellationToken = default)
        {
            return _client.Transport.SendFileAsync(
                HttpMethod.Post,
                _client.Path("api/render/{tenantId}/file"),
                BuildRequestJson(),
                cancellationToken);
        }

        /// <summary>
        /// Emite el documento y devuelve sus datos (id, URLs, versión del
        /// diseño) sin descargarlo. Útil para guardarlo en el ERP o para
        /// compartirlo después con <see cref="RendersetClient.Share"/>.
        /// </summary>
        public async Task<RenderedDocument> EmitAsync(
            CancellationToken cancellationToken = default)
        {
            var dto = await _client.Transport.SendJsonAsync<DocumentDto>(
                HttpMethod.Post,
                _client.Path("api/render/{tenantId}"),
                BuildRequestJson(),
                cancellationToken).ConfigureAwait(false);

            return new RenderedDocument(dto);
        }

        /// <summary>
        /// Comprueba la petición sin emitir nada: que el report existe, que
        /// las filas encajan con el mapping y que hay diseño para ese
        /// contexto. Los errores son los mismos que daría el render.
        /// </summary>
        public async Task<RenderValidation> ValidateAsync(
            CancellationToken cancellationToken = default)
        {
            var dto = await _client.Transport.SendJsonAsync<ValidationDto>(
                HttpMethod.Post,
                _client.Path("api/render/{tenantId}/validate"),
                BuildRequestJson(),
                cancellationToken).ConfigureAwait(false);

            return new RenderValidation(dto);
        }

        /// <summary>
        /// Versión síncrona para WinForms y código antiguo.
        ///
        /// Usa Task.Run a propósito: llamar a .Result directamente desde el
        /// hilo de UI de WinForms bloquea la aplicación para siempre, porque
        /// la continuación espera un contexto que está ocupado esperándola.
        /// </summary>
        public RenderResult Generate()
        {
            return Task.Run(() => GenerateAsync())
                .GetAwaiter()
                .GetResult();
        }

        /// <inheritdoc cref="EmitAsync"/>
        public RenderedDocument Emit()
        {
            return Task.Run(() => EmitAsync())
                .GetAwaiter()
                .GetResult();
        }

        /// <inheritdoc cref="ValidateAsync"/>
        public RenderValidation Validate()
        {
            return Task.Run(() => ValidateAsync())
                .GetAwaiter()
                .GetResult();
        }

        // ------------------------------------------------------------ interno

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
                    "Se han indicado filas y JSON a la vez. Elige uno.");
            }

            if (_rows != null && _rows.RowCount == 0)
            {
                throw new InvalidOperationException(
                    "La consulta no ha devuelto ninguna fila.");
            }
        }

        /// <summary>
        /// Cuerpo de la petición: el mismo para render, fichero y validación,
        /// y el que va dentro de un enlace compartido que emite documentos.
        /// </summary>
        internal string BuildRequestJson()
        {
            EnsureValidRequest();

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

            builder.Append(",\"output\":{\"format\":");
            JsonWriter.WriteString(builder, _format);

            if (!string.IsNullOrEmpty(_fileName))
            {
                builder.Append(",\"fileName\":");
                JsonWriter.WriteString(builder, _fileName);
            }

            if (_includeData)
                builder.Append(",\"includeDocumentData\":true");

            builder.Append('}');

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
}
