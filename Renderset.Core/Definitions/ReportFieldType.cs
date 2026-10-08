namespace Renderset.Core.Definitions;

public enum ReportFieldType
{
    Text,
    Number,
    Currency,
    Date,
    Percentage,
    Email,
    Boolean,

    /// <summary>
    /// Número sin decimales (1.480). Para cantidades, que con "Number"
    /// salían como 16,00.
    /// </summary>
    Integer
}