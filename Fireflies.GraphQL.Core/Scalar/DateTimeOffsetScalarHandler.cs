using System.Globalization;
using System.Text.Json;

namespace Fireflies.GraphQL.Core.Scalar;

public class DateTimeOffsetScalarHandler : IScalarHandler {
    public Type BaseType => typeof(DateTimeOffset);

    public void Serialize(Utf8JsonWriter writer, object value) {
        writer.WriteStringValue(((DateTimeOffset)value).ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz", DateTimeFormatInfo.InvariantInfo));
    }

    public void Serialize(Utf8JsonWriter writer, string property, object value) {
        writer.WriteString(property, ((DateTimeOffset)value).ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz", DateTimeFormatInfo.InvariantInfo));
    }

    public object? Deserialize(object value, Type type) {
        return DateTimeOffset.Parse(value.ToString()!);
    }
}