using System.Text.RegularExpressions;

namespace Renderset.Deca.Validation;

/// <summary>
/// Matrículas. Las españolas se comprueban; las extranjeras (cabotaje) no
/// tienen un formato común, así que sólo se pide que parezcan una matrícula
/// y se avisa para que alguien la revise.
/// </summary>
public static partial class VehiclePlate
{
    public enum Kind
    {
        Invalid,

        /// <summary>
        /// 1234BCD (desde 2000).
        /// </summary>
        Spanish,

        /// <summary>
        /// R1234BCD: remolques y semirremolques.
        /// </summary>
        SpanishTrailer,

        /// <summary>
        /// Provincial anterior a 2000: M1234AB, B123456.
        /// </summary>
        SpanishLegacy,

        /// <summary>
        /// No sigue ningún formato español pero tiene forma de matrícula.
        /// </summary>
        Other
    }

    /// <summary>
    /// Mayúsculas y sin espacios ni guiones: 1234-BCD → 1234BCD.
    /// </summary>
    public static string Normalize(
        string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : new string(
                value
                    .Where(char.IsLetterOrDigit)
                    .Select(char.ToUpperInvariant)
                    .ToArray());

    public static Kind Classify(
        string? value)
    {
        var plate = Normalize(value);

        if (plate.Length is < 2 or > 12)
            return Kind.Invalid;

        if (Current().IsMatch(plate))
            return Kind.Spanish;

        if (Trailer().IsMatch(plate))
            return Kind.SpanishTrailer;

        if (Legacy().IsMatch(plate))
            return Kind.SpanishLegacy;

        // Al menos una cifra: "CAMION" no es una matrícula.
        return plate.Any(char.IsDigit)
            ? Kind.Other
            : Kind.Invalid;
    }

    /// <summary>
    /// Sin vocales ni Ñ ni Q: las letras que usa el sistema actual.
    /// </summary>
    [GeneratedRegex("^[0-9]{4}[BCDFGHJKLMNPRSTVWXYZ]{3}$")]
    private static partial Regex Current();

    [GeneratedRegex("^R[0-9]{4}[BCDFGHJKLMNPRSTVWXYZ]{3}$")]
    private static partial Regex Trailer();

    /// <summary>
    /// Una o dos letras de provincia, cuatro a seis cifras y hasta dos
    /// letras.
    /// </summary>
    [GeneratedRegex("^[A-Z]{1,2}[0-9]{4,6}[A-Z]{0,2}$")]
    private static partial Regex Legacy();
}
