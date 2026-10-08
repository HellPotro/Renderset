# Renderset.Client

Genera documentos con RenderSet desde cualquier aplicación .NET: envías el
resultado de tu consulta tal cual y recibes el PDF. También emite sin
descargar, valida sin emitir, vuelve a descargar documentos y crea enlaces
para compartir con clientes.

- **netstandard2.0**: .NET Framework 4.6.1 en adelante (WinForms incluido),
  .NET 6, 8 y 10.
- **Sin dependencias**: ni System.Text.Json ni Newtonsoft, así que no hay
  binding redirects en proyectos antiguos.
- Activa TLS 1.2 solo en .NET Framework 4.5/4.6.

## Instalar

Mientras no esté publicado en un feed de NuGet:

```xml
<!-- Referencia de proyecto -->
<ProjectReference Include="..\Renderset\Renderset.Client\Renderset.Client.csproj" />
```

o como paquete local:

```
dotnet pack Renderset.Client -c Release -o C:\nuget-local
dotnet nuget add source C:\nuget-local --name renderset-local
dotnet add package Renderset.Client --version 2.0.0
```

## Crear el cliente

Uno para toda la aplicación (es seguro entre hilos). La clave se crea en
RenderSet → Configuración → API keys y va en la configuración, no en el código.

```csharp
using Renderset.Client;

var renderset = RendersetClient.Create(
    "https://api.renderset.miempresa.com",
    ConfigurationManager.AppSettings["Renderset:ApiKey"],   // rs_live_…
    "oran");                                                  // tenant de la clave
```

En un servicio, con tu `HttpClient` (por ejemplo de `IHttpClientFactory`), que
ya traiga `BaseAddress` y la cabecera `X-Api-Key`:

```csharp
var renderset = RendersetClient.Create(httpClient, "oran");
```

## Generar un PDF desde tu consulta

```csharp
using (var connection = new SqlConnection(erpConnectionString))
using (var command = new SqlCommand(sqlPackingList, connection))
{
    command.Parameters.AddWithValue("@salida", 3129);

    var pdf = await renderset
        .Report("packing-list")
        .FromCommand(command)       // o .FromReader(reader) / .FromDataTable(table)
        .ForCustomer("002297")      // opcional: diseño asignado a ese cliente
        .WithCulture("en-GB")       // opcional: idioma, números y fechas
        .GenerateAsync();

    pdf.Save(@"C:\salidas");        // con el nombre que propone el servidor
}
```

Tu consulta no cambia: las columnas de cabecera repetidas por el JOIN se
envían repetidas y el mapping del report (RenderSet → Reports → Datos) las
agrupa en documento, albaranes y líneas. Los tipos salen del proveedor de
datos, no de mirar los valores: una referencia `00123` llega como texto.

Una llamada emite un documento. Si las filas traen dos, la API responde
`rows.multiple_documents`: filtra la consulta por la clave del documento.

### Fuentes de datos

```csharp
.FromReader(reader)       // IDataReader (consume el reader entero)
.FromDataTable(table)     // un DataTable ya cargado
.FromCommand(command)     // ejecuta el comando; abre y cierra la conexión si hace falta
.FromJson(json)           // el documento ya en JSON jerárquico, sin mapping
```

### Opciones

```csharp
.WithPreset("packing-list-geimex", version: 3)   // diseño y versión concretos
.ForCustomer("002297")                           // diseño asignado a ese cliente
.ForContext("supplier", "000045")                // otro tipo de asignación
.WithCulture("en-GB")                            // idioma y formatos
.WithMapping("packing-list-rows")                // sólo si el report tiene varios mappings
.WithFileName("salida-{{data.salidaid}}.html")   // nombre; en PDF pasa a .pdf
.AsHtml()                                        // HTML en vez de PDF
.IncludeData()                                   // CSV/JSON en el visor y en enlaces que lo permitan
```

## Terminar la petición

| Método | Qué hace | Devuelve |
| --- | --- | --- |
| `GenerateAsync()` | Emite y descarga el fichero | `RenderResult` (bytes, `FileName`, `DocumentId`) |
| `EmitAsync()` | Emite sin descargar | `RenderedDocument` (`DocumentId`, `Url`, `PdfUrl`, versión del diseño) |
| `ValidateAsync()` | Comprueba sin emitir ni guardar | `RenderValidation` (`Valid`, `Errors`, preset que se usaría) |

`Generate()`, `Emit()` y `Validate()` son las versiones síncronas, pensadas
para WinForms: usan `Task.Run` por dentro, porque un `.Result` en el hilo de
la interfaz bloquea la aplicación para siempre.

```csharp
private void btnImprimir_Click(object sender, EventArgs e)
{
    using (var command = new SqlCommand(sqlPackingList, _connection))
    {
        command.Parameters.AddWithValue("@salida", txtSalida.Text);

        var pdf = _renderset.Report("packing-list").FromCommand(command).Generate();
        var path = pdf.Save(Path.GetTempPath());

        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }
}
```

## Compartir con el cliente

Un enlace público con caducidad, con uno o varios documentos. Pueden estar ya
emitidos o emitirse en la misma llamada.

```csharp
var packingList = await renderset.Report("packing-list").FromCommand(cmdSalida).EmitAsync();

var link = await renderset
    .Share("Envío 3129 - Geimex")
    .WithDocument(packingList.DocumentId, "Packing list")
    .WithDocument(renderset.Report("factura").FromCommand(cmdFactura), "Factura")
    .InCulture("en-GB")          // idioma de la página
    .ExpiresInDays(30)           // sin esto, la caducidad del tenant
    .AllowDataDownload()         // opcional: CSV y JSON para el cliente
    .CreateAsync();

EnviarCorreo(cliente.Email, link.Url);
```

## Documentos ya emitidos

```csharp
var pdf  = await renderset.Documents.DownloadPdfAsync(documentId);
var html = await renderset.Documents.DownloadHtmlAsync(documentId);
var info = await renderset.Documents.GetAsync(documentId);

var page = await renderset.Documents.SearchAsync(reportId: "packing-list", take: 20);
// siguiente página: SearchAsync(..., cursor: page.NextCursor)
```

## Errores

```csharp
try
{
    var pdf = await builder.GenerateAsync();
}
catch (RendersetException ex)
{
    // ex.StatusCode: 400, 401 (sin clave), 403 (otro tenant), 503 (sin conversor de PDF)…
    // ex.Errors: [{ Code = "mapping.not_configured", Message = "…", Path = "mappingId" }]
    MessageBox.Show(string.Join(Environment.NewLine, ex.Errors));
}
```

Reintentos automáticos, con espera creciente (3 por defecto):

- Consultas y descargas (`Documents`): fallos de red, timeouts, 5xx y 429.
- Emisiones y enlaces (`GenerateAsync`, `EmitAsync`, `CreateAsync`): sólo 429 y
  fallos de conexión. Un timeout o un 5xx pueden llegar con el documento ya
  emitido (por ejemplo `pdf.generation_failed`: el HTML está guardado y falló
  el PDF), y repetir lo duplicaría. En ese caso descarga el PDF después con
  `Documents.DownloadPdfAsync`.
- Los 4xx nunca: si la petición está mal, repetirla no la arregla.

## Configuración

```csharp
var renderset = RendersetClient.Create(url, apiKey, tenant,
    new RendersetClientOptions
    {
        Timeout = TimeSpan.FromMinutes(10),
        MaxRetries = 5,
        MaxRows = 50_000      // la API acepta hasta 50.000 filas por petición
    });
```

## Cambios en 2.0

- `Create` pide el tenant (va en la ruta de la API).
- `GenerateAsync` usa `POST /api/render/{tenant}/file` y devuelve también el `DocumentId`.
- Nuevos: `EmitAsync`, `ValidateAsync` (devuelve `RenderValidation`), `IncludeData`,
  `Documents`, `Share`.
- `WithMapping` ya no es obligatorio con filas: se usa el mapping del report.
- `RendersetException.Errors` trae los errores ya leídos.
- Fuera: `AsMergedBatch`, `AsSeparateFiles` y `WithIdempotencyKey`, que la API
  no implementa.
