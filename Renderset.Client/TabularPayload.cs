using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using Renderset.Client.Internal;

namespace Renderset.Client
{
    /// <summary>
    /// Resultado tabular listo para enviar. Se construye recorriendo un
    /// IDataReader una sola vez.
    ///
    /// El reader del cliente vive en SU proceso y nunca cruza la red: lo que
    /// viaja es esto.
    /// </summary>
    internal sealed class TabularPayload
        : IJsonWritable
    {
        private readonly List<Column> _columns = new List<Column>();
        private readonly List<object[]> _rows = new List<object[]>();

        public int RowCount
        {
            get { return _rows.Count; }
        }

        public int ColumnCount
        {
            get { return _columns.Count; }
        }

        public static TabularPayload FromReader(
            IDataReader reader,
            int maxRows)
        {
            if (reader == null)
                throw new ArgumentNullException(nameof(reader));

            var payload = new TabularPayload();

            for (var i = 0; i < reader.FieldCount; i++)
            {
                payload._columns.Add(
                    new Column
                    {
                        Name = reader.GetName(i),
                        Type = MapType(reader.GetFieldType(i))
                    });
            }

            while (reader.Read())
            {
                if (maxRows > 0 && payload._rows.Count >= maxRows)
                {
                    throw new RendersetException(
                        0,
                        "La consulta ha devuelto más de " + maxRows +
                        " filas. Acota la consulta o sube MaxRows en las " +
                        "opciones del cliente.");
                }

                var values = new object[reader.FieldCount];

                for (var i = 0; i < reader.FieldCount; i++)
                {
                    var value = reader.GetValue(i);

                    values[i] = value == DBNull.Value
                        ? null
                        : value;
                }

                payload._rows.Add(values);
            }

            return payload;
        }

        public void WriteTo(
            StringBuilder builder)
        {
            builder.Append("{\"columns\":[");

            for (var i = 0; i < _columns.Count; i++)
            {
                if (i > 0)
                    builder.Append(',');

                builder.Append("{\"name\":");
                JsonWriter.WriteString(builder, _columns[i].Name);
                builder.Append(",\"type\":");
                JsonWriter.WriteString(builder, _columns[i].Type);
                builder.Append('}');
            }

            builder.Append("],\"rows\":[");

            for (var r = 0; r < _rows.Count; r++)
            {
                if (r > 0)
                    builder.Append(',');

                builder.Append('[');

                var row = _rows[r];

                for (var c = 0; c < row.Length; c++)
                {
                    if (c > 0)
                        builder.Append(',');

                    JsonWriter.WriteValue(builder, row[c]);
                }

                builder.Append(']');
            }

            builder.Append("]}");
        }

        /// <summary>
        /// El tipo se toma del proveedor, no se adivina mirando el valor. Es
        /// lo que evita que una referencia "00123" llegue al servidor como el
        /// número 123.
        /// </summary>
        private static string MapType(
            Type type)
        {
            var underlying =
                Nullable.GetUnderlyingType(type) ?? type;

            if (underlying == typeof(bool))
                return "boolean";

            if (underlying == typeof(DateTime) ||
                underlying == typeof(DateTimeOffset))
            {
                return "dateTime";
            }

            if (underlying == typeof(byte) ||
                underlying == typeof(sbyte) ||
                underlying == typeof(short) ||
                underlying == typeof(ushort) ||
                underlying == typeof(int) ||
                underlying == typeof(uint) ||
                underlying == typeof(long) ||
                underlying == typeof(ulong) ||
                underlying == typeof(float) ||
                underlying == typeof(double) ||
                underlying == typeof(decimal))
            {
                return "number";
            }

            return "string";
        }

        private sealed class Column
        {
            public string Name;
            public string Type;
        }
    }
}
