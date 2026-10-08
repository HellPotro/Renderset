namespace Renderset.Deca.Validation;

/// <summary>
/// Un problema de un DeCA. Los errores impiden emitir; los avisos no, pero
/// se enseñan (un NIF extranjero que no se puede comprobar, una matrícula
/// que no parece española).
/// </summary>
public sealed record DecaIssue(
    string Code,
    string Path,
    string Message,
    bool IsWarning = false);

public sealed class DecaValidationResult
{
    public List<DecaIssue> Issues { get; } = [];

    public IEnumerable<DecaIssue> Errors =>
        Issues.Where(x => !x.IsWarning);

    public IEnumerable<DecaIssue> Warnings =>
        Issues.Where(x => x.IsWarning);

    public bool IsValid =>
        !Errors.Any();

    /// <summary>
    /// Primer error de un campo, para pintarlo debajo de él.
    /// </summary>
    public DecaIssue? For(
        string path) =>
        Issues.FirstOrDefault(x => string.Equals(x.Path, path, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Validaciones de negocio del DeCA (Orden FOM/2861/2012, Resolución de 5
/// de junio de 2026). Es pura: la usan la API antes de emitir y el portal
/// mientras se rellena, con las mismas reglas.
///
/// Las rutas de los errores (shipper.taxId, shipments[0].weightKg) son las
/// del JSON de la API, para que el ERP sepa qué campo corregir.
/// </summary>
public static class DecaValidator
{
    public const int MaxShipments = 50;

    public const int MaxNameLength = 200;

    public const int MaxAddressLength = 300;

    public const int MaxPlaceLength = 300;

    public const int MaxGoodsLength = 300;

    public const int MaxTextLength = 2000;

    public const int MaxShortTextLength = 200;

    /// <param name="now">Momento de la emisión.</param>
    /// <param name="zone">Zona del documento (España): "hoy" y la hora de inicio son las de allí.</param>
    public static DecaValidationResult Validate(
        DecaData? data,
        DateTimeOffset now,
        TimeZoneInfo zone)
    {
        var result = new DecaValidationResult();

        if (data is null)
        {
            result.Issues.Add(new DecaIssue("deca.required", "", "Faltan los datos del DeCA."));
            return result;
        }

        ValidateParty(result, data.Shipper, "shipper", "del cargador contractual", requireAddress: true);
        ValidateParty(result, data.Carrier, "carrier", "del transportista efectivo", requireAddress: false);

        ValidateDates(result, data, now, zone);
        ValidateVehicle(result, data.Vehicle);

        MaxLength(result, data.SpecialAuthorization, "specialAuthorization", MaxShortTextLength, "La autorización especial");
        MaxLength(result, data.Observations, "observations", MaxTextLength, "Las observaciones");
        MaxLength(result, data.Reference, "reference", MaxShortTextLength, "La referencia");

        ValidateShipments(result, data.Shipments);

        return result;
    }

    // ------------------------------------------------------------- partes

    private static void ValidateParty(
        DecaValidationResult result,
        DecaParty? party,
        string path,
        string who,
        bool requireAddress)
    {
        party ??= new DecaParty();

        Required(result, party.Name, $"{path}.name", $"Falta la razón social {who}.");
        MaxLength(result, party.Name, $"{path}.name", MaxNameLength, "La razón social");

        if (string.IsNullOrWhiteSpace(party.TaxId))
        {
            result.Issues.Add(new DecaIssue("deca.required", $"{path}.taxId", $"Falta el NIF {who}."));
        }
        else if (!SpanishTaxId.IsValid(party.TaxId))
        {
            if (SpanishTaxId.LooksForeign(party.TaxId))
            {
                result.Issues.Add(new DecaIssue(
                    "deca.tax_id_foreign",
                    $"{path}.taxId",
                    $"El NIF {who} no es español: no se puede comprobar. Revísalo.",
                    IsWarning: true));
            }
            else
            {
                result.Issues.Add(new DecaIssue(
                    "deca.tax_id_invalid",
                    $"{path}.taxId",
                    $"El NIF {who} no es válido: revisa las cifras y la letra de control."));
            }
        }

        if (requireAddress)
            Required(result, party.Address, $"{path}.address", $"Falta el domicilio {who}.");

        MaxLength(result, party.Address, $"{path}.address", MaxAddressLength, "El domicilio");
    }

    // ------------------------------------------------------------- fechas

    /// <summary>
    /// El DeCA se emite antes de empezar el transporte. Con la fecha basta
    /// para no emitirlo para ayer; con la hora, para no emitirlo después de
    /// salir.
    /// </summary>
    private static void ValidateDates(
        DecaValidationResult result,
        DecaData data,
        DateTimeOffset now,
        TimeZoneInfo zone)
    {
        if (data.TransportDate is not { } date)
        {
            result.Issues.Add(new DecaIssue("deca.required", "transportDate", "Falta la fecha del transporte."));
            return;
        }

        var local = TimeZoneInfo.ConvertTime(now, zone);
        var today = DateOnly.FromDateTime(local.DateTime);

        if (date < today)
        {
            result.Issues.Add(new DecaIssue(
                "deca.transport_in_past",
                "transportDate",
                "La fecha del transporte ya ha pasado: el DeCA se emite antes de empezar el transporte."));

            return;
        }

        if (date == today &&
            data.StartTime is { } start &&
            start <= TimeOnly.FromDateTime(local.DateTime))
        {
            result.Issues.Add(new DecaIssue(
                "deca.transport_started",
                "startTime",
                $"La hora de inicio ({start:HH\\:mm}) ya ha pasado: el DeCA se emite antes de empezar el transporte."));
        }

        if (date > today.AddDays(365))
        {
            result.Issues.Add(new DecaIssue(
                "deca.transport_too_far",
                "transportDate",
                "La fecha del transporte es de dentro de más de un año. ¿Está bien el año?",
                IsWarning: true));
        }
    }

    // ------------------------------------------------------------ vehículo

    private static void ValidateVehicle(
        DecaValidationResult result,
        DecaVehicle? vehicle)
    {
        vehicle ??= new DecaVehicle();

        if (string.IsNullOrWhiteSpace(vehicle.TractorPlate))
        {
            result.Issues.Add(new DecaIssue(
                "deca.required",
                "vehicle.tractorPlate",
                "Falta la matrícula del vehículo tractor o rígido."));
        }
        else
        {
            Plate(result, vehicle.TractorPlate, "vehicle.tractorPlate", "del vehículo");
        }

        if (!string.IsNullOrWhiteSpace(vehicle.TrailerPlate))
            Plate(result, vehicle.TrailerPlate, "vehicle.trailerPlate", "del remolque");
    }

    private static void Plate(
        DecaValidationResult result,
        string value,
        string path,
        string what)
    {
        switch (VehiclePlate.Classify(value))
        {
            case VehiclePlate.Kind.Invalid:
                result.Issues.Add(new DecaIssue(
                    "deca.plate_invalid",
                    path,
                    $"La matrícula {what} no es válida."));
                break;

            case VehiclePlate.Kind.Other:
                result.Issues.Add(new DecaIssue(
                    "deca.plate_foreign",
                    path,
                    $"La matrícula {what} no sigue el formato español. Si es extranjera, está bien; si no, revísala.",
                    IsWarning: true));
                break;
        }
    }

    // -------------------------------------------------------------- envíos

    private static void ValidateShipments(
        DecaValidationResult result,
        List<DecaShipment>? shipments)
    {
        if (shipments is null || shipments.Count == 0)
        {
            result.Issues.Add(new DecaIssue(
                "deca.shipments_required",
                "shipments",
                "Falta al menos un envío con origen, destino y mercancía."));

            return;
        }

        if (shipments.Count > MaxShipments)
        {
            result.Issues.Add(new DecaIssue(
                "deca.shipments_too_many",
                "shipments",
                $"Un DeCA admite como mucho {MaxShipments} envíos."));
        }

        for (var i = 0; i < shipments.Count; i++)
        {
            var shipment = shipments[i] ?? new DecaShipment();
            var path = $"shipments[{i}]";
            var label = shipments.Count == 1 ? "" : $" del envío {i + 1}";

            Required(result, shipment.Origin, $"{path}.origin", $"Falta el lugar de origen{label}.");
            Required(result, shipment.Destination, $"{path}.destination", $"Falta el lugar de destino{label}.");
            Required(result, shipment.Goods, $"{path}.goods", $"Falta la naturaleza de la mercancía{label}.");

            MaxLength(result, shipment.Origin, $"{path}.origin", MaxPlaceLength, "El origen");
            MaxLength(result, shipment.Destination, $"{path}.destination", MaxPlaceLength, "El destino");
            MaxLength(result, shipment.Goods, $"{path}.goods", MaxGoodsLength, "La mercancía");
            MaxLength(result, shipment.Notes, $"{path}.notes", MaxTextLength, "Las notas");
            MaxLength(result, shipment.AlternativeUnit, $"{path}.alternativeUnit", 40, "La unidad");

            var hasWeight = shipment.WeightKg is > 0;
            var hasAlternative = shipment.AlternativeQuantity is > 0;

            if (shipment.WeightKg is <= 0)
            {
                result.Issues.Add(new DecaIssue(
                    "deca.weight_invalid",
                    $"{path}.weightKg",
                    $"El peso{label} tiene que ser mayor que cero."));
            }
            else if (!hasWeight && !hasAlternative)
            {
                result.Issues.Add(new DecaIssue(
                    "deca.weight_required",
                    $"{path}.weightKg",
                    $"Falta el peso{label} (o una magnitud sustitutiva con su unidad)."));
            }

            if (hasAlternative && string.IsNullOrWhiteSpace(shipment.AlternativeUnit))
            {
                result.Issues.Add(new DecaIssue(
                    "deca.unit_required",
                    $"{path}.alternativeUnit",
                    $"Falta la unidad de la magnitud sustitutiva{label}."));
            }

            if (shipment.WeightKg is > 60_000)
            {
                result.Issues.Add(new DecaIssue(
                    "deca.weight_high",
                    $"{path}.weightKg",
                    $"El peso{label} pasa de 60.000 kg. ¿Está en kilos?",
                    IsWarning: true));
            }
        }
    }

    // ------------------------------------------------------------ comunes

    private static void Required(
        DecaValidationResult result,
        string? value,
        string path,
        string message)
    {
        if (string.IsNullOrWhiteSpace(value))
            result.Issues.Add(new DecaIssue("deca.required", path, message));
    }

    private static void MaxLength(
        DecaValidationResult result,
        string? value,
        string path,
        int max,
        string what)
    {
        if (value is not null && value.Trim().Length > max)
        {
            result.Issues.Add(new DecaIssue(
                "deca.too_long",
                path,
                $"{what} admite como mucho {max} caracteres."));
        }
    }
}
