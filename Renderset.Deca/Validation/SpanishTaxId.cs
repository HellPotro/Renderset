namespace Renderset.Deca.Validation;

/// <summary>
/// NIF, NIE y CIF españoles con su carácter de control.
///
///   NIF persona física: 8 cifras + letra         12345678Z
///   NIF K/L/M:          letra + 7 cifras + letra  K1234567S
///   NIE:                X/Y/Z + 7 cifras + letra  X1234567L
///   NIF persona jurídica (antiguo CIF):
///                       letra + 7 cifras + control (cifra o letra) B12345674
///
/// Acepta el prefijo de país ES (NIF-IVA) y separadores (espacios, guiones,
/// puntos), que se quitan al normalizar.
/// </summary>
public static class SpanishTaxId
{
    private const string NifLetters = "TRWAGMYFPDXBNJZSQVHLCKE";

    private const string CifControlLetters = "JABCDEFGHI";

    public enum Kind
    {
        Invalid,
        Nif,
        Nie,
        Cif
    }

    /// <summary>
    /// Mayúsculas y sin separadores ni prefijo ES. Lo que se guarda y se
    /// imprime.
    /// </summary>
    public static string Normalize(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var cleaned =
            new string(
                value
                    .Where(char.IsLetterOrDigit)
                    .Select(char.ToUpperInvariant)
                    .ToArray());

        if (cleaned.Length == 11 && cleaned.StartsWith("ES", StringComparison.Ordinal))
            cleaned = cleaned[2..];

        return cleaned;
    }

    public static Kind Classify(
        string? value)
    {
        var id = Normalize(value);

        if (id.Length != 9)
            return Kind.Invalid;

        if (IsNif(id))
            return Kind.Nif;

        if (IsNie(id))
            return Kind.Nie;

        if (IsCif(id))
            return Kind.Cif;

        return Kind.Invalid;
    }

    public static bool IsValid(
        string? value) =>
        Classify(value) != Kind.Invalid;

    /// <summary>
    /// Parece un identificador fiscal extranjero (NIF-IVA de otro país
    /// comunitario: dos letras y lo demás). No se puede comprobar aquí;
    /// sirve para avisar en vez de rechazar en cabotaje.
    /// </summary>
    public static bool LooksForeign(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var cleaned =
            new string(value.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

        return cleaned.Length is >= 4 and <= 15 &&
               char.IsLetter(cleaned[0]) &&
               char.IsLetter(cleaned[1]) &&
               !cleaned.StartsWith("ES", StringComparison.Ordinal) &&
               cleaned.Skip(2).Any(char.IsDigit);
    }

    private static bool IsNif(
        string id)
    {
        // 12345678Z
        if (id[..8].All(char.IsDigit))
            return id[8] == NifLetters[int.Parse(id[..8]) % 23];

        // K, L, M: como un DNI con un 0 delante.
        if (id[0] is 'K' or 'L' or 'M' && id[1..8].All(char.IsDigit))
            return id[8] == NifLetters[int.Parse(id[1..8]) % 23];

        return false;
    }

    private static bool IsNie(
        string id)
    {
        if (id[0] is not ('X' or 'Y' or 'Z') || !id[1..8].All(char.IsDigit))
            return false;

        var prefix = id[0] switch
        {
            'X' => "0",
            'Y' => "1",
            _ => "2"
        };

        var number = int.Parse(prefix + id[1..8]);

        return id[8] == NifLetters[number % 23];
    }

    /// <summary>
    /// Persona jurídica. Control: suma de las cifras en posición par más,
    /// por cada cifra en posición impar, las cifras de su doble; el control
    /// es (10 - suma % 10) % 10, como cifra o como letra de "JABCDEFGHI"
    /// según el tipo de entidad.
    /// </summary>
    private static bool IsCif(
        string id)
    {
        const string entities = "ABCDEFGHJNPQRSUVW";

        if (!entities.Contains(id[0]) || !id[1..8].All(char.IsDigit))
            return false;

        var sum = 0;

        for (var i = 1; i <= 7; i++)
        {
            var digit = id[i] - '0';

            if (i % 2 == 0)
            {
                sum += digit;
            }
            else
            {
                var doubled = digit * 2;
                sum += doubled / 10 + doubled % 10;
            }
        }

        var control = (10 - sum % 10) % 10;
        var actual = id[8];

        // Con letra: entidades sin ánimo de lucro, organismos públicos,
        // extranjeras y establecimientos permanentes (P, Q, R, S, N, W).
        // Con cifra: sociedades anónimas y limitadas, comunidades de bienes...
        // (A, B, E, H). El resto admite las dos formas.
        return id[0] switch
        {
            'P' or 'Q' or 'R' or 'S' or 'N' or 'W' => actual == CifControlLetters[control],
            'A' or 'B' or 'E' or 'H' => actual == (char)('0' + control),
            _ => actual == CifControlLetters[control] || actual == (char)('0' + control)
        };
    }
}
