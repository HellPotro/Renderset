namespace Renderset.Deca;

/// <summary>
/// Contenido de un DeCA: lo que va en el papel. Es lo que rellena el
/// formulario, lo que manda el ERP por API y lo que se guarda (en JSON) con
/// cada versión.
///
/// El cargador y el transportista están a nivel de documento y los envíos
/// son una lista. Así la regla de agrupación (varios envíos sólo si
/// comparten cargador contractual y transportista efectivo) se cumple por
/// construcción: no hay forma de expresar un DeCA que la incumpla.
///
/// Normativa: Orden FOM/2861/2012 y Resolución de 5 de junio de 2026
/// (BOE-A-2026-12784).
/// </summary>
public sealed class DecaData
{
    /// <summary>
    /// Cargador contractual: razón social, NIF y domicilio. Responde de los
    /// datos de cargador, transportista, origen, destino y mercancía.
    /// </summary>
    public DecaParty Shipper { get; set; } = new();

    /// <summary>
    /// Transportista efectivo: razón social y NIF. Responde de vehículo,
    /// fecha y autorizaciones.
    /// </summary>
    public DecaParty Carrier { get; set; } = new();

    /// <summary>
    /// Fecha del transporte.
    /// </summary>
    public DateOnly? TransportDate { get; set; }

    /// <summary>
    /// Hora prevista de inicio (hora de España). Opcional; si se indica, la
    /// emisión tiene que ser anterior.
    /// </summary>
    public TimeOnly? StartTime { get; set; }

    public DecaVehicle Vehicle { get; set; } = new();

    /// <summary>
    /// Autorización especial de circulación, cuando sea necesaria.
    /// </summary>
    public string? SpecialAuthorization { get; set; }

    /// <summary>
    /// Observaciones o reservas de los intervinientes.
    /// </summary>
    public string? Observations { get; set; }

    public List<DecaShipment> Shipments { get; set; } = [];

    /// <summary>
    /// Referencia propia (número de salida, pedido...). No es dato legal,
    /// sirve para buscar y para el nombre del fichero.
    /// </summary>
    public string? Reference { get; set; }

    /// <summary>
    /// Copia independiente, para duplicar un DeCA o versionarlo sin
    /// compartir listas.
    ///
    /// Tolera nulos: el JSON de la API no respeta las anotaciones de
    /// nulabilidad, y un "shipper": null tiene que acabar en un 400 de
    /// validación, no en un 500.
    /// </summary>
    public DecaData Clone() =>
        new()
        {
            Shipper = Shipper?.Clone() ?? new DecaParty(),
            Carrier = Carrier?.Clone() ?? new DecaParty(),
            TransportDate = TransportDate,
            StartTime = StartTime,
            Vehicle = Vehicle?.Clone() ?? new DecaVehicle(),
            SpecialAuthorization = SpecialAuthorization,
            Observations = Observations,
            Reference = Reference,
            Shipments = Shipments?
                .Where(x => x is not null)
                .Select(x => x.Clone())
                .ToList() ?? []
        };
}

public sealed class DecaParty
{
    public string? Name { get; set; }

    public string? TaxId { get; set; }

    /// <summary>
    /// Domicilio. Obligatorio para el cargador; el transportista no lo
    /// necesita.
    /// </summary>
    public string? Address { get; set; }

    public DecaParty Clone() =>
        new()
        {
            Name = Name,
            TaxId = TaxId,
            Address = Address
        };
}

public sealed class DecaVehicle
{
    /// <summary>
    /// Matrícula del vehículo tractor o rígido.
    /// </summary>
    public string? TractorPlate { get; set; }

    /// <summary>
    /// Remolque o semirremolque, cuando corresponda.
    /// </summary>
    public string? TrailerPlate { get; set; }

    public DecaVehicle Clone() =>
        new()
        {
            TractorPlate = TractorPlate,
            TrailerPlate = TrailerPlate
        };
}

/// <summary>
/// Un envío dentro del DeCA. Cada uno identifica su origen, destino,
/// naturaleza y peso de la mercancía.
/// </summary>
public sealed class DecaShipment
{
    public string? Origin { get; set; }

    public string? Destination { get; set; }

    /// <summary>
    /// Naturaleza de la mercancía.
    /// </summary>
    public string? Goods { get; set; }

    public decimal? WeightKg { get; set; }

    /// <summary>
    /// Magnitud sustitutiva del peso cuando proceda (metros cúbicos,
    /// unidades, litros...). Si se usa, hace falta su unidad.
    /// </summary>
    public decimal? AlternativeQuantity { get; set; }

    public string? AlternativeUnit { get; set; }

    /// <summary>
    /// Lo que difiera de los demás envíos (referencia, observaciones).
    /// </summary>
    public string? Notes { get; set; }

    public DecaShipment Clone() =>
        new()
        {
            Origin = Origin,
            Destination = Destination,
            Goods = Goods,
            WeightKg = WeightKg,
            AlternativeQuantity = AlternativeQuantity,
            AlternativeUnit = AlternativeUnit,
            Notes = Notes
        };
}
