using FluentAssertions;
using Renderset.Deca;
using Renderset.Deca.Validation;

namespace Renderset.Tests.Deca;

public sealed class SpanishTaxIdTests
{
    [Theory]
    [InlineData("12345678Z", SpanishTaxId.Kind.Nif)]
    [InlineData("12.345.678-z", SpanishTaxId.Kind.Nif)]
    [InlineData("X1234567L", SpanishTaxId.Kind.Nie)]
    [InlineData("B12345674", SpanishTaxId.Kind.Cif)]
    [InlineData("ESB12345674", SpanishTaxId.Kind.Cif)]
    [InlineData("Q2826000H", SpanishTaxId.Kind.Cif)]
    public void Classify_ShouldAcceptValidIds(
        string value,
        SpanishTaxId.Kind expected)
    {
        SpanishTaxId.Classify(value).Should().Be(expected);
    }

    [Theory]
    [InlineData("12345678A")]
    [InlineData("B12345675")]
    [InlineData("X1234567A")]
    [InlineData("Q2826000I")]
    [InlineData("123456789")]
    [InlineData("")]
    public void Classify_ShouldRejectWrongControlCharacters(
        string value)
    {
        SpanishTaxId.IsValid(value).Should().BeFalse();
    }

    [Fact]
    public void LooksForeign_ShouldRecogniseOtherEuVatNumbers()
    {
        SpanishTaxId.LooksForeign("PT 123456789").Should().BeTrue();
        SpanishTaxId.LooksForeign("ES12345678Z").Should().BeFalse();
        SpanishTaxId.LooksForeign("12345678A").Should().BeFalse();
    }
}

public sealed class VehiclePlateTests
{
    [Theory]
    [InlineData("1234-BCD", VehiclePlate.Kind.Spanish)]
    [InlineData("1234 bcd", VehiclePlate.Kind.Spanish)]
    [InlineData("R-1234-BCD", VehiclePlate.Kind.SpanishTrailer)]
    [InlineData("M-1234-AB", VehiclePlate.Kind.SpanishLegacy)]
    [InlineData("1234BCA", VehiclePlate.Kind.Other)]
    [InlineData("AB-123-CD", VehiclePlate.Kind.Other)]
    [InlineData("CAMION", VehiclePlate.Kind.Invalid)]
    public void Classify_ShouldRecogniseFormats(
        string value,
        VehiclePlate.Kind expected)
    {
        VehiclePlate.Classify(value).Should().Be(expected);
    }
}

public sealed class DecaValidatorTests
{
    /// <summary>
    /// Jueves 8 de octubre de 2026, 10:00 en Madrid.
    /// </summary>
    private static readonly DateTimeOffset Now =
        new(2026, 10, 8, 10, 0, 0, TimeSpan.FromHours(2));

    private static readonly TimeZoneInfo Zone = new DecaOptions().Zone;

    internal static DecaData Valid() =>
        new()
        {
            Shipper = new DecaParty
            {
                Name = "Talleres Oran, S.L.U.",
                TaxId = "B12345674",
                Address = "Pol. Ind. Campollano, C/ C 12, 02007 Albacete"
            },
            Carrier = new DecaParty
            {
                Name = "Transportes Ejemplo, S.L.",
                TaxId = "Q2826000H"
            },
            TransportDate = new DateOnly(2026, 10, 8),
            StartTime = new TimeOnly(12, 0),
            Vehicle = new DecaVehicle
            {
                TractorPlate = "1234BCD",
                TrailerPlate = "R1234BCD"
            },
            Shipments =
            [
                new DecaShipment
                {
                    Origin = "Albacete",
                    Destination = "Valencia",
                    Goods = "Piezas de estampación",
                    WeightKg = 12500
                }
            ]
        };

    private static DecaValidationResult Validate(
        DecaData data) =>
        DecaValidator.Validate(data, Now, Zone);

    [Fact]
    public void Validate_ShouldAcceptACompleteDeca()
    {
        var result = Validate(Valid());

        result.IsValid.Should().BeTrue(string.Join(" · ", result.Issues.Select(x => x.Message)));
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void Validate_ShouldRequireTheLegalData()
    {
        var result = Validate(new DecaData());

        result.Errors.Select(x => x.Path)
            .Should().Contain(
            new[]
            {
                "shipper.name",
                "shipper.taxId",
                "shipper.address",
                "carrier.name",
                "carrier.taxId",
                "transportDate",
                "vehicle.tractorPlate",
                "shipments"
            });

        // El transportista no necesita domicilio.
        result.Errors.Select(x => x.Path).Should().NotContain("carrier.address");
    }

    [Fact]
    public void Validate_ShouldRejectATransportThatAlreadyStarted()
    {
        var yesterday = Valid();
        yesterday.TransportDate = new DateOnly(2026, 10, 7);

        Validate(yesterday).Errors.Should().ContainSingle(x => x.Code == "deca.transport_in_past");

        var earlierToday = Valid();
        earlierToday.StartTime = new TimeOnly(9, 30);

        Validate(earlierToday).Errors.Should().ContainSingle(x => x.Code == "deca.transport_started");
    }

    [Fact]
    public void Validate_ShouldUseSpanishTimeForToday()
    {
        // 23:30 UTC del 7 ya es día 8 en Madrid: un transporte del 8 vale.
        var lateNightUtc = new DateTimeOffset(2026, 10, 7, 23, 30, 0, TimeSpan.Zero);
        var data = Valid();
        data.StartTime = null;

        DecaValidator.Validate(data, lateNightUtc, Zone).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ShouldRequireWeightOrAlternativeQuantity()
    {
        var data = Valid();
        data.Shipments[0].WeightKg = null;

        Validate(data).Errors.Should().ContainSingle(x =>
            x.Code == "deca.weight_required" && x.Path == "shipments[0].weightKg");

        data.Shipments[0].AlternativeQuantity = 33;

        Validate(data).Errors.Should().ContainSingle(x =>
            x.Code == "deca.unit_required" && x.Path == "shipments[0].alternativeUnit");

        data.Shipments[0].AlternativeUnit = "m³";

        Validate(data).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ShouldOnlyWarnForForeignIdsAndPlates()
    {
        var data = Valid();
        data.Carrier.TaxId = "PT123456789";
        data.Vehicle.TractorPlate = "AB-123-CD";

        var result = Validate(data);

        result.IsValid.Should().BeTrue();
        result.Warnings.Select(x => x.Code)
            .Should().BeEquivalentTo(new[] { "deca.tax_id_foreign", "deca.plate_foreign" });
    }

    [Fact]
    public void Validate_ShouldRejectAWrongNif()
    {
        var data = Valid();
        data.Shipper.TaxId = "B12345675";

        Validate(data).Errors.Should().ContainSingle(x =>
            x.Code == "deca.tax_id_invalid" && x.Path == "shipper.taxId");
    }

    [Fact]
    public void Normalizer_ShouldCleanWhatIsPrinted()
    {
        var data = Valid();
        data.Shipper.TaxId = "es-b12345674";
        data.Vehicle.TractorPlate = " 1234-bcd ";
        data.Shipments.Add(new DecaShipment());
        data.Observations = "   ";

        var normalized = DecaNormalizer.Normalize(data);

        normalized.Shipper.TaxId.Should().Be("B12345674");
        normalized.Vehicle.TractorPlate.Should().Be("1234BCD");
        normalized.Shipments.Should().ContainSingle();
        normalized.Observations.Should().BeNull();

        // El original no se toca.
        data.Shipments.Should().HaveCount(2);
    }
}

public sealed class DecaAvailabilityTests
{
    private static readonly DecaOptions Options = new();

    [Fact]
    public void PublicUntil_ShouldLastSevenFullDaysInSpain()
    {
        // Termina el 8 a las 15:00 en Madrid (13:00 UTC): el QR descarga
        // hasta el final del día 15, medianoche del 16 en Madrid.
        var ended = new DateTime(2026, 10, 8, 13, 0, 0, DateTimeKind.Utc);

        DecaAvailability.PublicUntil(ended, Options)
            .Should().Be(new DateTime(2026, 10, 15, 22, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void PublicUntil_ShouldNeverBeShorterThanTheLegalMinimum()
    {
        var options = new DecaOptions { PublicDaysAfterService = 1, RetentionDays = 30 };
        var ended = new DateTime(2026, 10, 8, 13, 0, 0, DateTimeKind.Utc);

        DecaAvailability.PublicUntil(ended, options)
            .Should().Be(DecaAvailability.PublicUntil(ended, Options));

        DecaAvailability.RetainUntil(ended, options)
            .Should().Be(ended.AddDays(365));
    }

    [Fact]
    public void IsPublicDownloadActive_ShouldStayOnUntilTheServiceEnds()
    {
        var deca =
            new DecaDocument
            {
                Id = "a",
                TenantId = "oranauto",
                Number = "DECA-2026-000001",
                PublicCode = DecaPublicCode.New(),
                Data = new DecaData()
            };

        var now = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        DecaAvailability.IsPublicDownloadActive(deca, now).Should().BeTrue();

        deca.PublicUntilUtc = now;

        DecaAvailability.IsPublicDownloadActive(deca, now).Should().BeFalse();
        DecaAvailability.IsPublicDownloadActive(deca, now.AddSeconds(-1)).Should().BeTrue();
    }

    [Fact]
    public void PublicUrl_ShouldBeShortAndHttps()
    {
        var code = DecaPublicCode.New();

        var url = new DecaOptions { PublicBaseUrl = "https://deca.renderset.app/" }.PublicUrl(code);

        url.Should().Be($"https://deca.renderset.app/q/{code}");
        url!.Length.Should().BeLessThan(60);

        new DecaOptions { PublicBaseUrl = "http://deca.renderset.app" }.PublicUrl(code).Should().BeNull();
        new DecaOptions { PublicBaseUrl = "http://localhost:7001" }.PublicUrl(code).Should().NotBeNull();
        new DecaOptions().PublicUrl(code).Should().BeNull();
    }

    [Fact]
    public void PublicCode_ShouldBeRandomAndWellFormed()
    {
        var codes = Enumerable.Range(0, 200).Select(_ => DecaPublicCode.New()).ToList();

        codes.Should().OnlyContain(x => DecaPublicCode.IsWellFormed(x));
        codes.Distinct().Should().HaveCount(codes.Count);

        DecaPublicCode.IsWellFormed("../../etc/passwd").Should().BeFalse();
        DecaPublicCode.IsWellFormed(null).Should().BeFalse();
    }
}
