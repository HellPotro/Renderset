using System;
using System.Collections;
using System.Globalization;
using System.Text;

namespace Renderset.Client.Internal
{
    /// <summary>
    /// Escritor de JSON mínimo. Existe para que el paquete no dependa de
    /// System.Text.Json ni de Newtonsoft: en un proyecto .NET Framework
    /// antiguo, cada dependencia es un binding redirect potencial y la causa
    /// número uno de que alguien no consiga instalar tu paquete.
    ///
    /// Sólo escribe. No lee. Es todo lo que hace falta.
    /// </summary>
    internal static class JsonWriter
    {
        public static void WriteValue(
            StringBuilder builder,
            object value)
        {
            if (value == null || value == DBNull.Value)
            {
                builder.Append("null");
                return;
            }

            switch (value)
            {
                case string text:
                    WriteString(builder, text);
                    return;

                case bool flag:
                    builder.Append(flag ? "true" : "false");
                    return;

                case DateTime date:
                    WriteString(
                        builder,
                        date.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture));
                    return;

                case DateTimeOffset offset:
                    WriteString(
                        builder,
                        offset.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture));
                    return;

                case Guid guid:
                    WriteString(builder, guid.ToString());
                    return;

                case byte[] bytes:
                    WriteString(builder, Convert.ToBase64String(bytes));
                    return;

                case decimal number:
                    builder.Append(number.ToString(CultureInfo.InvariantCulture));
                    return;

                case double number:
                    builder.Append(number.ToString("R", CultureInfo.InvariantCulture));
                    return;

                case float number:
                    builder.Append(number.ToString("R", CultureInfo.InvariantCulture));
                    return;

                case byte or sbyte or short or ushort or int or uint or long or ulong:
                    builder.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                    return;

                case IJsonWritable writable:
                    writable.WriteTo(builder);
                    return;

                case IDictionary dictionary:
                    WriteObject(builder, dictionary);
                    return;

                case IEnumerable sequence:
                    WriteArray(builder, sequence);
                    return;

                default:
                    // Tipos raros de proveedor (SqlGeography, enums propios)
                    // acaban como texto. Es mejor que reventar.
                    WriteString(
                        builder,
                        Convert.ToString(value, CultureInfo.InvariantCulture));
                    return;
            }
        }

        public static void WriteObject(
            StringBuilder builder,
            IDictionary values)
        {
            builder.Append('{');

            var first = true;

            foreach (DictionaryEntry entry in values)
            {
                if (entry.Value == null)
                    continue;

                if (!first)
                    builder.Append(',');

                first = false;

                WriteString(builder, Convert.ToString(entry.Key));
                builder.Append(':');
                WriteValue(builder, entry.Value);
            }

            builder.Append('}');
        }

        public static void WriteArray(
            StringBuilder builder,
            IEnumerable values)
        {
            builder.Append('[');

            var first = true;

            foreach (var item in values)
            {
                if (!first)
                    builder.Append(',');

                first = false;

                WriteValue(builder, item);
            }

            builder.Append(']');
        }

        public static void WriteString(
            StringBuilder builder,
            string value)
        {
            if (value == null)
            {
                builder.Append("null");
                return;
            }

            builder.Append('"');

            foreach (var character in value)
            {
                switch (character)
                {
                    case '"':
                        builder.Append("\\\"");
                        break;

                    case '\\':
                        builder.Append("\\\\");
                        break;

                    case '\n':
                        builder.Append("\\n");
                        break;

                    case '\r':
                        builder.Append("\\r");
                        break;

                    case '\t':
                        builder.Append("\\t");
                        break;

                    case '\b':
                        builder.Append("\\b");
                        break;

                    case '\f':
                        builder.Append("\\f");
                        break;

                    default:
                        if (character < ' ')
                        {
                            builder.Append("\\u");
                            builder.Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            builder.Append(character);
                        }

                        break;
                }
            }

            builder.Append('"');
        }
    }

    internal interface IJsonWritable
    {
        void WriteTo(
            StringBuilder builder);
    }
}
