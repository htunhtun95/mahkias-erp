using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mahkias.Api.Helpers
{
    public sealed class WholeQuantityConverter : JsonConverter<int?>
    {
        public override int? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            if (reader.TokenType == JsonTokenType.Number)
            {
                if (!reader.TryGetDecimal(out var number) || number != decimal.Truncate(number) || number is < int.MinValue or > int.MaxValue)
                {
                    throw new JsonException("Quantity must be a whole number.");
                }

                return (int)number;
            }

            if (reader.TokenType == JsonTokenType.String)
            {
                var text = reader.GetString()?.Trim();
                if (string.IsNullOrEmpty(text))
                {
                    return null;
                }

                if (int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
                {
                    return value;
                }
            }

            throw new JsonException("Quantity must be a whole number.");
        }

        public override void Write(Utf8JsonWriter writer, int? value, JsonSerializerOptions options)
        {
            if (value.HasValue)
            {
                writer.WriteNumberValue(value.Value);
                return;
            }

            writer.WriteNullValue();
        }
    }

    public sealed class DecimalQuantityConverter : JsonConverter<decimal?>
    {
        public override decimal? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            if (reader.TokenType == JsonTokenType.Number)
            {
                if (!reader.TryGetDecimal(out var number))
                {
                    throw new JsonException("Quantity must be a number.");
                }

                return number;
            }

            if (reader.TokenType == JsonTokenType.String)
            {
                var text = reader.GetString()?.Trim();
                if (string.IsNullOrEmpty(text))
                {
                    return null;
                }

                if (decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value)
                    && text.All(character => char.IsDigit(character) || character == '.'))
                {
                    return value;
                }
            }

            throw new JsonException("Quantity must be a number.");
        }

        public override void Write(Utf8JsonWriter writer, decimal? value, JsonSerializerOptions options)
        {
            if (value.HasValue)
            {
                writer.WriteNumberValue(value.Value);
                return;
            }

            writer.WriteNullValue();
        }
    }
}
