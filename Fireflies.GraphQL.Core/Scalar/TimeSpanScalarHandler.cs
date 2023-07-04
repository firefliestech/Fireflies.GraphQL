using System.Text.Json;

namespace Fireflies.GraphQL.Core.Scalar;

public class TimeSpanScalarHandler : IScalarHandler {
    public Type BaseType => typeof(TimeSpan);

    public void Serialize(Utf8JsonWriter writer, object value) {
        writer.WriteStringValue(((TimeSpan)value).ToString());
    }

    public void Serialize(Utf8JsonWriter writer, string property, object value) {
        writer.WriteString(property, ((TimeSpan)value).ToString());
    }

    public object? Deserialize(object value, Type type) {
        return TimeSpan.Parse(value.ToString()!);
    }
}