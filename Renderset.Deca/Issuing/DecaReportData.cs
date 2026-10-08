using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Renderset.Deca.Issuing;

/// <summary>
/// Datos del documento para la plantilla. Todo va ya como texto formateado
/// en español ("12.500 kg", "08/10/2026"): lo que se imprime en un documento
/// legal no debe depender del formato de un campo del diseñador.
///
/// Lo que no aplica sale como "—" en vez de en blanco: en una inspección un
/// hueco parece un dato que falta; una raya dice que no corresponde.
/// </summary>
public static class DecaReportData
{
    public const string NotApplicable = "—";

    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-ES");

    public static JsonElement Build(
        DecaData data,
        string number,
        int version,
        DateTimeOffset issuedAtLocal,
        string? publicUrl)
    {
        ArgumentNullException.ThrowIfNull(data);

        var root = new JsonObject
        {
            ["numero"] = number,
            ["version"] = version.ToString(CultureInfo.InvariantCulture),
            ["emitido"] = issuedAtLocal.ToString("dd/MM/yyyy HH:mm", Spanish),

            ["cargador"] = new JsonObject
            {
                ["razonSocial"] = Text(data.Shipper.Name),
                ["nif"] = Text(data.Shipper.TaxId),
                ["domicilio"] = Text(data.Shipper.Address)
            },

            ["transportista"] = new JsonObject
            {
                ["razonSocial"] = Text(data.Carrier.Name),
                ["nif"] = Text(data.Carrier.TaxId)
            },

            ["transporte"] = new JsonObject
            {
                ["fecha"] = data.TransportDate is { } date
                    ? date.ToString("dd/MM/yyyy", Spanish)
                    : NotApplicable,
                ["inicio"] = data.StartTime is { } start
                    ? start.ToString("HH:mm", Spanish)
                    : NotApplicable,
                ["tractora"] = Text(data.Vehicle.TractorPlate),
                ["remolque"] = Text(data.Vehicle.TrailerPlate),
                ["autorizacionEspecial"] = Text(data.SpecialAuthorization),
                ["referencia"] = Text(data.Reference)
            },

            ["envios"] = new JsonArray(
                data.Shipments
                    .Select((shipment, index) => (JsonNode)new JsonObject
                    {
                        ["numero"] = (index + 1).ToString(CultureInfo.InvariantCulture),
                        ["origen"] = Text(shipment.Origin),
                        ["destino"] = Text(shipment.Destination),
                        ["mercancia"] = Text(shipment.Goods),
                        ["cantidad"] = Quantity(shipment),
                        ["notas"] = Text(shipment.Notes)
                    })
                    .ToArray()),

            ["observaciones"] = new JsonObject
            {
                ["texto"] = Text(data.Observations)
            },

            ["emision"] = new JsonObject
            {
                ["numero"] = number,
                ["fechaHora"] = issuedAtLocal.ToString("dd/MM/yyyy HH:mm:ss (zzz)", Spanish),
                ["version"] = version.ToString(CultureInfo.InvariantCulture),
                ["enlace"] = Text(publicUrl)
            }
        };

        using var document = JsonDocument.Parse(root.ToJsonString());

        return document.RootElement.Clone();
    }

    /// <summary>
    /// "12.500 kg"; con magnitud sustitutiva, "33 m³" y, si también hay
    /// peso, los dos.
    /// </summary>
    public static string Quantity(
        DecaShipment shipment)
    {
        var parts = new List<string>(2);

        if (shipment.WeightKg is decimal weight && weight > 0)
            parts.Add($"{weight.ToString("#,##0.###", Spanish)} kg");

        if (shipment.AlternativeQuantity is decimal quantity && quantity > 0)
        {
            parts.Add(
                $"{quantity.ToString("#,##0.###", Spanish)} {shipment.AlternativeUnit?.Trim()}".TrimEnd());
        }

        return parts.Count == 0
            ? NotApplicable
            : string.Join(" · ", parts);
    }

    private static string Text(
        string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? NotApplicable
            : value.Trim();
}
