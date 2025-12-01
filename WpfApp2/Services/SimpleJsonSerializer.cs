using System;
using System.Collections;
using System.Reflection;
using System.Text;

namespace BackupServer.Services
{
    public static class SimpleJsonSerializer
    {
        public static string Serialize(object obj)
        {
            if (obj == null) return "null";

            var sb = new StringBuilder();
            SerializeObject(obj, sb);
            return sb.ToString();
        }

        private static void SerializeObject(object obj, StringBuilder sb)
        {
            if (obj == null)
            {
                sb.Append("null");
                return;
            }

            var type = obj.GetType();

            if (type == typeof(string))
            {
                sb.Append($"\"{EscapeString(obj.ToString())}\"");
            }
            else if (type == typeof(bool))
            {
                sb.Append(obj.ToString().ToLower());
            }
            else if (type == typeof(DateTime))
            {
                sb.Append($"\"{obj}\"");
            }
            else if (IsNumericType(type))
            {
                sb.Append(obj.ToString());
            }
            else if (type.IsArray || (obj is IEnumerable && !(obj is string)))
            {
                SerializeArray(obj as IEnumerable, sb);
            }
            else if (type.IsClass || type.IsValueType)
            {
                SerializeAnonymousObject(obj, sb);
            }
            else
            {
                sb.Append($"\"{EscapeString(obj.ToString())}\"");
            }
        }

        private static void SerializeArray(IEnumerable enumerable, StringBuilder sb)
        {
            sb.Append("[");
            bool first = true;
            foreach (var item in enumerable)
            {
                if (!first) sb.Append(",");
                SerializeObject(item, sb);
                first = false;
            }
            sb.Append("]");
        }

        private static void SerializeAnonymousObject(object obj, StringBuilder sb)
        {
            sb.Append("{");
            var properties = obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            
            for (int i = 0; i < properties.Length; i++)
            {
                var prop = properties[i];
                sb.Append($"\"{prop.Name}\":");
                SerializeObject(prop.GetValue(obj, null), sb);
                if (i < properties.Length - 1) sb.Append(",");
            }
            sb.Append("}");
        }

        private static bool IsNumericType(Type type)
        {
            return type == typeof(int) || type == typeof(long) || type == typeof(float) || 
                   type == typeof(double) || type == typeof(decimal) || type == typeof(short) ||
                   type == typeof(byte) || type == typeof(uint) || type == typeof(ulong) ||
                   type == typeof(ushort) || type == typeof(sbyte);
        }

        private static string EscapeString(string str)
        {
            if (string.IsNullOrEmpty(str)) return str;
            
            return str.Replace("\\", "\\\\")
                     .Replace("\"", "\\\"")
                     .Replace("\n", "\\n")
                     .Replace("\r", "\\r")
                     .Replace("\t", "\\t")
                     .Replace("\b", "\\b")
                     .Replace("\f", "\\f");
        }
    }
}