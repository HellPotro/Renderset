# Renderset.Client

Genera documentos con RenderSet desde cualquier aplicación .NET. Envía
directamente el resultado de tu consulta, sin transformar los datos.

Compatible con **.NET Framework 4.6.1 en adelante**, .NET Core, .NET 5 y
posteriores. **Sin dependencias**: no arrastra System.Text.Json ni Newtonsoft,
así que se instala en proyectos antiguos sin pelearse con binding redirects.

```
Install-Package Renderset.Client
```

## Uso

```csharp
using Renderset.Client;

using (var connection = new SqlConnection(erpConnectionString))
using (var command = new SqlCommand(sqlFactura, connection))
{
    command.Parameters.AddWithValue("@numero", "10001");
    connection.Open();

    using (var reader = command.ExecuteReader())
    {
        var pdf = await RendersetClient
            .Create("https://api.renderset.io", "rs_live_...")
            .Report("invoice")
            .WithMapping("invoice-erp-sql")
            .FromReader(reader)
            .WithCulture("es-ES")
            .GenerateAsync();

        pdf.Save(@"C:\facturas");
    }
}
```

Tu consulta no cambia. Las columnas de cabecera repetidas por el JOIN se
envían repetidas: agruparlas en cabecera y líneas es trabajo del servidor.

## Desde WinForms

```csharp
private void btnImprimir_Click(object sender, EventArgs e)
{
    using (var command = new SqlCommand(sqlFactura, _connection))
    {
        command.Parameters.AddWithValue("@numero", txtFactura.Text);

        var pdf = _renderset
            .Report("invoice")
            .WithMapping("invoice-erp-sql")
            .FromCommand(command)
            .Generate();

        var path = Path.Combine(Path.GetTempPath(), pdf.FileName);

        pdf.Save(path);

        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }
}
```

`Generate()` es la versión síncrona, pensada para código antiguo. Internamente
usa `Task.Run`, porque llamar a `.Result` desde el hilo de UI de WinForms
bloquea la aplicación para siempre.

## Otras formas de enviar datos

```csharp
.FromDataTable(table)     // un DataTable ya cargado
.FromCommand(command)     // ejecuta el comando y consume el reader
.FromJson(jsonString)     // modelo jerárquico ya montado
```

## Opciones

```csharp
.WithPreset("invoice-cliente-a", version: 3)  // diseño y versión concretos
.ForCustomer("000123")                        // que el servidor elija el diseño
.WithCulture("en-GB")                         // idioma y formatos
.WithFileName("factura-{{data.number}}.pdf")
.AsMergedBatch()                              // varias facturas en un PDF
.AsSeparateFiles()                            // un ZIP con una por fichero
.WithIdempotencyKey(numeroFactura)            // evita duplicados al reintentar
```

## Validar antes de generar

```csharp
var resultado = builder.Validate();
```

Comprueba los datos contra el mapping sin generar nada. Dice qué columna falta
o qué tipo no cuadra, en vez de dejarte un PDF con huecos.

## Errores

```csharp
try
{
    var pdf = await builder.GenerateAsync();
}
catch (RendersetException ex)
{
    // ex.StatusCode y ex.Body traen el motivo real del servidor
    MessageBox.Show(ex.Body);
}
```

Los fallos de red y los errores 5xx se reintentan solos con espera creciente
(tres intentos por defecto). Los 4xx no: si la petición está mal, repetirla no
la arregla.

## Configuración

```csharp
var client = RendersetClient.Create(baseUrl, apiKey,
    new RendersetClientOptions
    {
        Timeout = TimeSpan.FromMinutes(10),
        MaxRetries = 5,
        MaxRows = 500_000
    });
```

En un servicio de larga vida, reutiliza tu `HttpClient`:

```csharp
var client = RendersetClient.Create(httpClientDeLaApp);
```

## Nota sobre TLS

En .NET Framework 4.5 y 4.6, TLS 1.2 no está activo por defecto y la conexión
falla con un mensaje que no explica nada. El cliente lo activa solo al
arrancar, no tienes que hacer nada.
