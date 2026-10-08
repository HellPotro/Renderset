using Renderset.Deca.Validation;

namespace Renderset.Deca;

/// <summary>
/// Deja los datos como se imprimen: textos recortados, NIF y matrículas en
/// mayúsculas y sin separadores, envíos vacíos fuera. Se aplica antes de
/// validar y de emitir, así que lo que se guarda es exactamente lo que va en
/// el PDF.
/// </summary>
public static class DecaNormalizer
{
    public static DecaData Normalize(
        DecaData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var copy = data.Clone();

        copy.Shipper = Party(copy.Shipper);
        copy.Carrier = Party(copy.Carrier);

        copy.Vehicle.TractorPlate = Plate(copy.Vehicle.TractorPlate);
        copy.Vehicle.TrailerPlate = Plate(copy.Vehicle.TrailerPlate);

        copy.SpecialAuthorization = Text(copy.SpecialAuthorization);
        copy.Observations = Text(copy.Observations);
        copy.Reference = Text(copy.Reference);

        copy.Shipments =
            (copy.Shipments ?? [])
                .Where(x => x is not null)
                .Select(x =>
                {
                    x.Origin = Text(x.Origin);
                    x.Destination = Text(x.Destination);
                    x.Goods = Text(x.Goods);
                    x.AlternativeUnit = Text(x.AlternativeUnit);
                    x.Notes = Text(x.Notes);

                    return x;
                })
                .Where(x => !IsBlank(x))
                .ToList();

        return copy;
    }

    /// <summary>
    /// Un envío sin nada escrito: la fila vacía que deja el formulario. Se
    /// descarta igual en el portal que en la API, para que los dos validen
    /// lo mismo.
    /// </summary>
    public static bool IsBlank(
        DecaShipment? shipment) =>
        shipment is null ||
        (string.IsNullOrWhiteSpace(shipment.Origin) &&
         string.IsNullOrWhiteSpace(shipment.Destination) &&
         string.IsNullOrWhiteSpace(shipment.Goods) &&
         string.IsNullOrWhiteSpace(shipment.AlternativeUnit) &&
         string.IsNullOrWhiteSpace(shipment.Notes) &&
         shipment.WeightKg is null &&
         shipment.AlternativeQuantity is null);

    private static DecaParty Party(
        DecaParty? party) =>
        new()
        {
            Name = Text(party?.Name),
            Address = Text(party?.Address),

            // Uno extranjero se deja tal cual (salvo espacios): cada país
            // escribe el suyo a su manera.
            TaxId = SpanishTaxId.IsValid(party?.TaxId)
                ? SpanishTaxId.Normalize(party?.TaxId)
                : Text(party?.TaxId)?.ToUpperInvariant()
        };

    private static string? Plate(
        string? value)
    {
        var plate = VehiclePlate.Normalize(value);

        return plate.Length == 0
            ? null
            : plate;
    }

    private static string? Text(
        string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
}
