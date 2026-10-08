namespace Renderset.Web.Components.Pages;

/// <summary>
/// Ejemplos de la página API (librería .NET y HTTP). Van aquí y no en el
/// .razor porque son bloques de código con XML y llaves: en C# son texto y
/// nada más.
/// </summary>
public partial class ApiPage
{
    private sealed record Sample(
        string Anchor,
        string Title,
        string Intro,
        string Code,
        string? Note = null,
        bool Wide = false);

    // ------------------------------------------------------------ librería

    private IReadOnlyList<Sample> LibrarySamples =>
    [
        new("instalar",
            "Instalar",
            "Renderset.Client es un proyecto netstandard2.0 sin dependencias: vale para .NET Framework 4.6.1+, WinForms, .NET 6, 8 y 10. Mientras no esté en un feed de NuGet, se añade como referencia de proyecto o como paquete local.",
            """
            <!-- Opción A: referencia de proyecto (en el .csproj de tu aplicación) -->
            <ProjectReference Include="..\Renderset\Renderset.Client\Renderset.Client.csproj" />

            # Opción B: paquete local
            dotnet pack Renderset.Client -c Release -o C:\nuget-local
            dotnet nuget add source C:\nuget-local --name renderset-local

            # y en tu proyecto
            dotnet add package Renderset.Client --version 2.0.0
            # o, en Visual Studio: Install-Package Renderset.Client -Version 2.0.0
            """,
            "En un proyecto .NET Framework antiguo (packages.config) usa la opción B: Visual Studio resuelve solo las referencias de netstandard."),

        new("cliente",
            "Crear el cliente",
            "Uno para toda la aplicación: es seguro usarlo desde varios hilos. La clave va en la configuración, nunca en el código.",
            $$"""
            using Renderset.Client;

            var renderset = RendersetClient.Create(
                "{{ApiUrl}}",
                ConfigurationManager.AppSettings["Renderset:ApiKey"],   // rs_live_…
                "{{Tenant}}");

            // En ASP.NET Core / servicios, con tu HttpClient (IHttpClientFactory):
            //   http.BaseAddress = new Uri("{{ApiUrl}}/");
            //   http.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
            //   var renderset = RendersetClient.Create(http, "{{Tenant}}");
            """),

        new("sql",
            "Generar un PDF desde tu consulta",
            "Envías el resultado del SELECT tal cual: las columnas de cabecera repetidas por el JOIN van repetidas, y el mapping del report (Reports → Datos) las agrupa en documento, albaranes y líneas.",
            """
            using (var connection = new SqlConnection(erpConnectionString))
            using (var command = new SqlCommand(sqlPackingList, connection))
            {
                command.Parameters.AddWithValue("@salida", 3129);

                var pdf = await renderset
                    .Report("packing-list")
                    .FromCommand(command)        // o .FromReader(reader) / .FromDataTable(table)
                    .ForCustomer("002297")      // opcional: el diseño asignado a ese cliente
                    .WithCulture("en-GB")       // opcional: idioma, números y fechas
                    .GenerateAsync();

                pdf.Save(@"C:\salidas");         // con el nombre que propone el servidor
                // pdf.DocumentId → id en RenderSet, para volver a descargarlo o compartirlo
            }
            """,
            "Una llamada = un documento. Si las filas traen dos salidas, la API responde rows.multiple_documents: filtra la consulta por la clave del documento."),

        new("winforms",
            "Desde WinForms (síncrono)",
            "Generate(), Emit() y Validate() tienen versión síncrona pensada para el hilo de la interfaz: no bloquean la aplicación como un .Result.",
            """
            private void btnImprimir_Click(object sender, EventArgs e)
            {
                using (var command = new SqlCommand(sqlPackingList, _connection))
                {
                    command.Parameters.AddWithValue("@salida", txtSalida.Text);

                    var pdf = _renderset
                        .Report("packing-list")
                        .FromCommand(command)
                        .Generate();

                    var path = pdf.Save(Path.GetTempPath());

                    Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                }
            }
            """),

        new("compartir",
            "Emitir y compartir con el cliente",
            "EmitAsync emite sin descargar y devuelve el id. Share crea un enlace público con caducidad con uno o varios documentos; también puede emitirlos en la misma llamada.",
            """
            var packingList = await renderset
                .Report("packing-list")
                .FromCommand(cmdSalida)
                .IncludeData()                 // CSV/JSON en el visor, si el enlace lo permite
                .EmitAsync();

            var link = await renderset
                .Share("Envío 3129 - Geimex")
                .WithDocument(packingList.DocumentId, "Packing list")
                .WithDocument(renderset.Report("factura").FromCommand(cmdFactura), "Factura")
                .InCulture("en-GB")            // idioma de la página
                .ExpiresInDays(30)
                .AllowDataDownload()           // opcional
                .CreateAsync();

            EnviarCorreo(cliente.Email, link.Url);
            """),

        new("validar",
            "Validar antes de poner en producción",
            "Hace todo lo del render (report, mapping, filas, diseño) sin guardar nada. Los errores son los mismos que daría el render.",
            """
            var check = await renderset
                .Report("packing-list")
                .FromCommand(command)
                .ValidateAsync();

            if (!check.Valid)
            {
                foreach (var error in check.Errors)
                    Console.WriteLine(error);   // rows.invalid (rows): Falta la columna 'Peso'.
            }
            """),

        new("documentos",
            "Volver a descargar un documento",
            "Los documentos emitidos son inmutables: lo que se descarga es lo que se envió, aunque el diseño haya cambiado después.",
            """
            var pdf = await renderset.Documents.DownloadPdfAsync(documentId);
            var info = await renderset.Documents.GetAsync(documentId);   // report, diseño y versión, idioma…

            var page = await renderset.Documents.SearchAsync(reportId: "packing-list", take: 20);
            foreach (var item in page.Items)
                Console.WriteLine($"{item.CreatedAtUtc:g} {item.FileName}");
            // siguiente página: SearchAsync(..., cursor: page.NextCursor)
            """),

        new("errores",
            "Errores y reintentos",
            "Un 4xx lanza RendersetException con los errores ya leídos. Las descargas se reintentan solas ante fallos de red y 5xx; las emisiones sólo ante 429 o fallo de conexión, para no duplicar documentos.",
            """
            try
            {
                var pdf = await builder.GenerateAsync();
            }
            catch (RendersetException ex)
            {
                // ex.StatusCode → 400, 401, 403…
                // ex.Errors     → [{ Code = "mapping.not_configured", Message = "…", Path = "mappingId" }]
                MessageBox.Show(string.Join(Environment.NewLine, ex.Errors));
            }

            var renderset = RendersetClient.Create(url, apiKey, tenant,
                new RendersetClientOptions
                {
                    Timeout = TimeSpan.FromMinutes(10),
                    MaxRetries = 5,
                    MaxRows = 50_000       // la API acepta hasta 50.000 filas por petición
                });
            """)
    ];

    // ------------------------------------------------------------ HTTP

    private IReadOnlyList<Sample> HttpSamples =>
    [
        new("render-rows",
            "Emitir con las filas del SELECT",
            "POST /api/render/{tenantId} con 'rows'. Se aplica el mapping del report; 'mappingId' sólo si tiene varios. Responde 201 con documentId, url y pdfUrl.",
            $$"""
            POST {{ApiUrl}}/api/render/{{Tenant}}
            X-Api-Key: rs_live_…
            Content-Type: application/json

            {
              "reportId": "packing-list",
              "context": { "culture": "en-GB", "contextType": "customer", "contextKey": "002297" },
              "output": { "format": "Pdf", "includeDocumentData": true },
              "rows": {
                "columns": [
                  { "name": "Salida", "type": "number" },
                  { "name": "Cliente", "type": "string" },
                  { "name": "Jaula", "type": "string" },
                  { "name": "Peso", "type": "number" }
                ],
                "rows": [
                  [3129, "002297", "PAL234923", 122.97],
                  [3129, "002297", "PAL234924", 71.02]
                ]
              }
            }

            → 201 { "documentId": "…", "url": "…", "pdfUrl": "…/pdf", "presetVersion": 4, … }
            """,
            Wide: true),

        new("render-file",
            "Emitir y recibir el PDF",
            "El mismo cuerpo en /file: la respuesta es el fichero. El id del documento viene en X-Renderset-Document-Id.",
            $$"""
            POST {{ApiUrl}}/api/render/{{Tenant}}/file
            X-Api-Key: rs_live_…
            Content-Type: application/json

            { "reportId": "packing-list", "data": { … } }

            → 200 application/pdf
              Content-Disposition: attachment; filename=packing-list.pdf
              X-Renderset-Document-Id: 8c1f…
            """),

        new("validate",
            "Validar sin emitir",
            "El mismo cuerpo en /validate. Siempre 200: 'valid' dice si emitiría.",
            $$"""
            POST {{ApiUrl}}/api/render/{{Tenant}}/validate
            X-Api-Key: rs_live_…

            { "reportId": "packing-list", "rows": { … } }

            → 200 {
                "valid": false,
                "reportId": "packing-list",
                "errors": [
                  { "code": "rows.invalid", "message": "Falta la columna 'Peso'.", "path": "rows" }
                ]
              }
            """),

        new("bundle",
            "Compartir documentos",
            "Un enlace público con caducidad. Los documentos pueden estar emitidos (documentId) o emitirse aquí (render).",
            $$"""
            POST {{ApiUrl}}/api/bundles/{{Tenant}}
            X-Api-Key: rs_live_…

            {
              "title": "Envío 3129 - Geimex",
              "culture": "en-GB",
              "expiresInDays": 30,
              "allowDataDownload": false,
              "items": [
                { "documentId": "8c1f…", "displayName": "Packing list" },
                { "render": { "reportId": "factura", "data": { … } }, "displayName": "Factura" }
              ]
            }

            → 201 { "bundleId": "…", "url": "https://…/share/…", "expiresAtUtc": "…", … }
            """),

        new("errors",
            "Errores",
            "400 con la lista de errores; 401 sin clave; 403 con la clave de otro tenant; 503 si se pide PDF y no hay conversor.",
            """
            → 400 [
                {
                  "code": "rows.multiple_documents",
                  "message": "Las filas producen 2 documentos y cada petición emite uno. Envía las filas de un solo valor de Salida.",
                  "path": "rows"
                }
              ]

            Códigos frecuentes: report.not_found, preset.not_found, mapping.not_configured,
            rows.invalid, rows.invalid_value, rows.no_documents, rows.multiple_documents,
            request.data_required, pdf.not_configured, auth.api_key_required, auth.tenant_mismatch
            """,
            Wide: true)
    ];

}
