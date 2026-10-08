namespace Renderset.Deca.Issuing;

/// <summary>
/// Un DeCA de ejemplo para el diseñador: con dos envíos, remolque y
/// observaciones, para que se vea todo lo que puede salir.
/// </summary>
public static class DecaSample
{
    public const string Number = "DECA-2026-000123";

    public static DecaData Data(
        DateOnly transportDate) =>
        new()
        {
            Shipper = new DecaParty
            {
                Name = "Talleres Ejemplo, S.L.",
                TaxId = "B12345674",
                Address = "Pol. Ind. Campollano, Calle C 12, 02007 Albacete"
            },
            Carrier = new DecaParty
            {
                Name = "Transportes del Levante, S.A.",
                TaxId = "A87654323"
            },
            TransportDate = transportDate,
            StartTime = new TimeOnly(7, 30),
            Vehicle = new DecaVehicle
            {
                TractorPlate = "1234BCD",
                TrailerPlate = "R5678BCD"
            },
            Reference = "Salida 3129",
            Observations = "Descarga en muelle 4. Llamar 30 minutos antes de llegar.",
            Shipments =
            [
                new DecaShipment
                {
                    Origin = "Albacete (02007)",
                    Destination = "Almussafes, Valencia (46440)",
                    Goods = "Piezas de estampación (paneles laterales)",
                    WeightKg = 12500
                },
                new DecaShipment
                {
                    Origin = "Albacete (02007)",
                    Destination = "Martorell, Barcelona (08760)",
                    Goods = "Conjuntos soldados",
                    WeightKg = 4800,
                    AlternativeQuantity = 22,
                    AlternativeUnit = "jaulas",
                    Notes = "Entrega antes de las 14:00"
                }
            ]
        };
}
