using System.Data;
using Renderset.Core.Reports;

namespace Renderset.Core.DataSources;

/// <summary>
/// Adaptador de System.Data.IDataReader. Es lo que permite el escenario
/// "tengo mi comando y mi reader, no me hagas pasar por JSON":
///
///   using var reader = command.ExecuteReader();
///   await RenderSet.Create("invoice").FromReader(reader).GenerateAsync();
/// </summary>
public sealed class DataReaderDataSet
    : IDataSetReader
{
    private readonly IDataReader _reader;
    private readonly bool _ownsReader;

    public DataReaderDataSet(
        IDataReader reader,
        bool ownsReader = false)
    {
        ArgumentNullException.ThrowIfNull(reader);

        _reader = reader;
        _ownsReader = ownsReader;

        Schema = BuildSchema(reader);
    }

    public DataSetSchema Schema { get; }

    public ValueTask<bool> ReadAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult(_reader.Read());
    }

    public object? GetValue(
        int ordinal)
    {
        var value = _reader.GetValue(ordinal);

        return value == DBNull.Value
            ? null
            : value;
    }

    public ValueTask DisposeAsync()
    {
        if (_ownsReader)
            _reader.Dispose();

        return ValueTask.CompletedTask;
    }

    private static DataSetSchema BuildSchema(
        IDataReader reader)
    {
        var columns = new List<DataSetColumn>(reader.FieldCount);

        for (var i = 0; i < reader.FieldCount; i++)
        {
            var type = reader.GetFieldType(i);

            columns.Add(
                new DataSetColumn
                {
                    Name = reader.GetName(i),
                    Ordinal = i,
                    Type = MapType(type),
                    NativeType = type.Name
                });
        }

        return new DataSetSchema
        {
            Columns = columns
        };
    }

    /// <summary>
    /// En SQL el tipo viene dado, así que no hace falta adivinarlo mirando
    /// los valores como sí hay que hacer con CSV.
    /// </summary>
    public static ReportDataType MapType(
        Type type)
    {
        var underlying =
            Nullable.GetUnderlyingType(type) ?? type;

        if (underlying == typeof(bool))
            return ReportDataType.Boolean;

        if (underlying == typeof(DateTime) ||
            underlying == typeof(DateTimeOffset))
        {
            return ReportDataType.DateTime;
        }

        if (underlying == typeof(DateOnly))
            return ReportDataType.Date;

        if (underlying == typeof(byte) ||
            underlying == typeof(short) ||
            underlying == typeof(int) ||
            underlying == typeof(long) ||
            underlying == typeof(float) ||
            underlying == typeof(double) ||
            underlying == typeof(decimal))
        {
            return ReportDataType.Number;
        }

        return ReportDataType.String;
    }
}
