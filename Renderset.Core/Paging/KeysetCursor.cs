using System.Text;
using System.Text.Json;

namespace Renderset.Core.Paging;

/// <summary>
/// Cursor para listados ordenados por fecha de creación descendente.
///
/// Se pagina por "lo anterior a esta fecha" y no con número de página: el
/// coste no crece con lo lejos que se navegue, y si mientras tanto se emiten
/// documentos nuevos no se repiten ni se saltan filas en la página
/// siguiente, que es lo que pasa con OFFSET.
///
/// Además de la fecha guarda los ids de la última página que tenían esa
/// misma fecha exacta. Dos filas con el mismo instante son rarísimas
/// (datetime2 tiene precisión de 100 ns), pero si pasa, la siguiente página
/// las excluye por id en vez de perderlas o repetirlas. Así no hace falta
/// comparar ids con "menor que", que con Guid no se puede traducir a SQL de
/// forma fiable.
/// </summary>
public sealed class KeysetCursor
{
    public required DateTime CreatedAtUtc { get; init; }

    public required IReadOnlyList<string> SeenIds { get; init; }

    public string Encode()
    {
        var payload =
            JsonSerializer.SerializeToUtf8Bytes(
                new Payload
                {
                    T = CreatedAtUtc.Ticks,
                    I = SeenIds.ToArray()
                });

        return Convert.ToBase64String(payload)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    /// <summary>
    /// Nulo si el cursor no es válido: lo trae el cliente y puede venir
    /// truncado o inventado.
    /// </summary>
    public static KeysetCursor? TryDecode(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 4096)
            return null;

        try
        {
            var base64 =
                value
                    .Replace('-', '+')
                    .Replace('_', '/');

            base64 = base64.PadRight(
                base64.Length + (4 - base64.Length % 4) % 4,
                '=');

            var payload =
                JsonSerializer.Deserialize<Payload>(
                    Convert.FromBase64String(base64));

            if (payload is null ||
                payload.T <= 0 ||
                payload.T > DateTime.MaxValue.Ticks)
            {
                return null;
            }

            return new KeysetCursor
            {
                CreatedAtUtc = new DateTime(payload.T, DateTimeKind.Utc),
                SeenIds = payload.I ?? []
            };
        }
        catch (Exception ex) when (
            ex is FormatException or JsonException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// Arma la página a partir de lo leído. Quien llama pide
    /// <paramref name="take"/> + 1 filas: si llega la fila de más, hay otra
    /// página, y la fila sobrante no se devuelve.
    /// </summary>
    public static PagedResult<T> Page<T>(
        IReadOnlyList<T> rows,
        int take,
        Func<T, DateTime> createdAtUtc,
        Func<T, string> id)
    {
        if (rows.Count <= take)
        {
            return new PagedResult<T>
            {
                Items = rows
            };
        }

        var items = rows.Take(take).ToList();
        var last = createdAtUtc(items[^1]);

        var seen =
            items
                .Where(x => createdAtUtc(x) == last)
                .Select(id)
                .ToList();

        return new PagedResult<T>
        {
            Items = items,
            NextCursor = new KeysetCursor
            {
                CreatedAtUtc = last,
                SeenIds = seen
            }.Encode()
        };
    }

    private sealed class Payload
    {
        public long T { get; set; }

        public string[]? I { get; set; }
    }
}
