using System.Text.Json;

namespace Fireflies.GraphQL.Core.Scalar;

public class ObjectScalarHandler : IScalarHandler {
    public Type BaseType => typeof(string);

    public void Serialize(Utf8JsonWriter writer, object value) {
        writer.WriteStringValue(JsonSerializer.Serialize(value));
    }

    public void Serialize(Utf8JsonWriter writer, string property, object value) {
        writer.WriteString(property, JsonSerializer.Serialize(value));
    }

    public object? Deserialize(object value, Type type) {
        return null;
    }
}