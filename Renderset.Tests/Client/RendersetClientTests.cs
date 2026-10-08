using System.Data;
using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Renderset.Client;

namespace Renderset.Tests.ClientLibrary;

/// <summary>
/// El cliente habla con la API real: rutas con tenant, cuerpo que entiende
/// RenderRequest y respuestas que sabe leer sin System.Text.Json.
/// </summary>
public sealed class RendersetClientTests
{
    private sealed class Recorder : HttpMessageHandler
    {
        public List<(HttpMethod Method, string Path, string? Body)> Requests { get; } = [];

        public Func<HttpRequestMessage, HttpResponseMessage> Respond { get; set; } =
            _ => new HttpResponseMessage(HttpStatusCode.OK);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add((
                request.Method,
                request.RequestUri!.PathAndQuery,
                request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken)));

            return Respond(request);
        }
    }

    private static (RendersetClient Client, Recorder Handler) Create()
    {
        var handler = new Recorder();

        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.test/") };

        return (RendersetClient.Create(http, "oran", new RendersetClientOptions { MaxRetries = 0 }), handler);
    }

    private static DataTable Salida()
    {
        var table = new DataTable();
        table.Columns.Add("Salida", typeof(int));
        table.Columns.Add("Cliente", typeof(string));
        table.Columns.Add("Peso", typeof(decimal));

        table.Rows.Add(3129, "002297", 122.97m);
        table.Rows.Add(3129, "002297", 71.02m);

        return table;
    }

    [Fact]
    public async Task Generate_ShouldPostToTheFileEndpointWithTheTenantAndReturnTheFile()
    {
        var (client, handler) = Create();

        handler.Respond = _ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([37, 80, 68, 70])
            };

            response.Content.Headers.ContentType = new("application/pdf");
            response.Content.Headers.ContentDisposition = new("attachment") { FileName = "salida-3129.pdf" };
            response.Headers.Add("X-Renderset-Document-Id", "abc123");

            return response;
        };

        var result =
            await client.Report("packing-list")
                .FromDataTable(Salida())
                .ForCustomer("002297")
                .WithCulture("en-GB")
                .IncludeData()
                .GenerateAsync();

        result.IsPdf.Should().BeTrue();
        result.FileName.Should().Be("salida-3129.pdf");
        result.DocumentId.Should().Be("abc123");

        var request = handler.Requests.Single();
        request.Path.Should().Be("/api/render/oran/file");

        // El cuerpo tiene que poder leerlo la API como RenderRequest.
        using var body = JsonDocument.Parse(request.Body!);
        var root = body.RootElement;

        root.GetProperty("reportId").GetString().Should().Be("packing-list");
        root.GetProperty("context").GetProperty("contextType").GetString().Should().Be("customer");
        root.GetProperty("context").GetProperty("culture").GetString().Should().Be("en-GB");
        root.GetProperty("output").GetProperty("format").GetString().Should().Be("Pdf");
        root.GetProperty("output").GetProperty("includeDocumentData").GetBoolean().Should().BeTrue();

        var rows = root.GetProperty("rows");
        rows.GetProperty("columns")[1].GetProperty("type").GetString().Should().Be("string");
        rows.GetProperty("rows").GetArrayLength().Should().Be(2);
        rows.GetProperty("rows")[0][1].GetString().Should().Be("002297");
        rows.GetProperty("rows")[0][2].GetDecimal().Should().Be(122.97m);
    }

    [Fact]
    public async Task Emit_ShouldReadTheDocumentWithoutSystemTextJson()
    {
        var (client, handler) = Create();

        handler.Respond = _ => Json(HttpStatusCode.Created,
            """
            {"documentId":"abc123","url":"https://api.test/api/documents/oran/abc123","pdfUrl":"https://api.test/api/documents/oran/abc123/pdf",
             "fileName":"salida-3129.html","reportId":"packing-list","presetId":"packing-list-default","presetVersion":4,
             "culture":"en-GB","format":"Pdf","createdAtUtc":"2026-10-06T14:09:00.123Z"}
            """);

        var document =
            await client.Report("packing-list")
                .FromJson("""{ "salidaid": 3129 }""")
                .EmitAsync();

        handler.Requests.Single().Path.Should().Be("/api/render/oran");

        document.DocumentId.Should().Be("abc123");
        document.PresetVersion.Should().Be(4);
        document.PdfUrl.Should().EndWith("/pdf");
        document.CreatedAtUtc.Should().Be(new DateTime(2026, 10, 6, 14, 9, 0, 123, DateTimeKind.Utc));
    }

    [Fact]
    public async Task Errors_ShouldComeBackAsTypedErrors()
    {
        var (client, handler) = Create();

        handler.Respond = _ => Json(HttpStatusCode.BadRequest,
            """[{"code":"mapping.not_configured","message":"El report 'x' no tiene mapping.","path":"mappingId"}]""");

        var act = () => client.Report("x").FromDataTable(Salida()).EmitAsync();

        var exception = (await act.Should().ThrowAsync<RendersetException>()).Which;

        exception.StatusCode.Should().Be(400);
        exception.Errors.Should().ContainSingle()
            .Which.Code.Should().Be("mapping.not_configured");
        exception.Message.Should().Contain("no tiene mapping");
    }

    [Fact]
    public async Task Validate_ShouldUseTheValidateEndpoint()
    {
        var (client, handler) = Create();

        handler.Respond = _ => Json(HttpStatusCode.OK,
            """{"valid":false,"reportId":"x","errors":[{"code":"rows.invalid","message":"Falta la columna 'Peso'.","path":"rows"}]}""");

        var validation = await client.Report("x").FromDataTable(Salida()).ValidateAsync();

        handler.Requests.Single().Path.Should().Be("/api/render/oran/validate");
        validation.Valid.Should().BeFalse();
        validation.Errors.Single().Path.Should().Be("rows");
    }

    [Fact]
    public async Task Share_ShouldMixEmittedAndNewDocuments()
    {
        var (client, handler) = Create();

        handler.Respond = _ => Json(HttpStatusCode.Created,
            """
            {"bundleId":"8f2c3a52-7a8b-4c5d-9e0f-1a2b3c4d5e6f","url":"https://api.test/share/8f2c/tok","title":"Envío 3129",
             "culture":"en-GB","status":"Active","allowDataDownload":true,"expiresAtUtc":"2026-11-05T14:09:00Z",
             "documents":[{"position":2,"documentId":"new1","displayName":"Factura"},{"position":1,"documentId":"abc123","displayName":"Packing list"}]}
            """);

        var link =
            await client.Share("Envío 3129")
                .WithDocument("abc123", "Packing list")
                .WithDocument(client.Report("factura").FromJson("""{ "numero": "F-1" }"""), "Factura")
                .InCulture("en-GB")
                .ExpiresInDays(30)
                .AllowDataDownload()
                .CreateAsync();

        link.Url.Should().StartWith("https://api.test/share/");
        link.DocumentIds.Should().Equal("abc123", "new1");

        var request = handler.Requests.Single();
        request.Path.Should().Be("/api/bundles/oran");

        using var body = JsonDocument.Parse(request.Body!);
        var items = body.RootElement.GetProperty("items");

        items[0].GetProperty("documentId").GetString().Should().Be("abc123");
        items[1].GetProperty("render").GetProperty("reportId").GetString().Should().Be("factura");
        body.RootElement.GetProperty("allowDataDownload").GetBoolean().Should().BeTrue();
        body.RootElement.GetProperty("expiresInDays").GetInt32().Should().Be(30);
    }

    private static HttpResponseMessage Json(
        HttpStatusCode status,
        string json) =>
        new(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
}
