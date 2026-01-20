using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InfiniteWin
{
    /// <summary>
    /// JSON converter for IntPtr to allow serialization
    /// </summary>
    public class IntPtrConverter : JsonConverter<IntPtr>
    {
        public override IntPtr Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Number)
            {
                return new IntPtr(reader.GetInt64());
            }
            else if (reader.TokenType == JsonTokenType.String)
            {
                string? value = reader.GetString();
                if (long.TryParse(value, out long result))
                {
                    return new IntPtr(result);
                }
            }
            return IntPtr.Zero;
        }

        public override void Write(Utf8JsonWriter writer, IntPtr value, JsonSerializerOptions options)
        {
            writer.WriteNumberValue(value.ToInt64());
        }
    }
}
