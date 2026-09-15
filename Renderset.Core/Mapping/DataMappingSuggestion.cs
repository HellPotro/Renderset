namespace Renderset.Core.Mapping;

public sealed class DataMappingSuggestion
{
    public required DataMapping Mapping { get; init; }

    public IReadOnlyList<DataMappingKeyCandidate> Candidates { get; init; } = [];

    /// <summary>
    /// Niveles detectados, de la raíz hacia abajo.
    /// </summary>
    public IReadOnlyList<DataMappingLevel> Levels { get; init; } = [];

    public int Confidence { get; init; }

    /// <summary>
    /// Explicación en lenguaje llano de por qué se ha propuesto esto. Una
    /// sugerencia que no se puede cuestionar es peor que ninguna.
    /// </summary>
    public string Reason { get; init; } = string.Empty;
}
