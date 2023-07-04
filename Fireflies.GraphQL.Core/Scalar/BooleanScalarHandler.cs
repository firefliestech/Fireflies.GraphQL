using System.Text.Json;

namespace Fireflies.GraphQL.Core.Scalar;

public class BooleanScalarHandler : IScalarHandler {
    public Type BaseType => typeof(bool);

    public void Serialize(Utf8JsonWriter writer, object value) {
        writer.WriteBooleanValue(Convert.ToBoolean(value));
    }

    public void Serialize(Utf8JsonWriter writer, string property, object value) {
        writer.WriteBoolean(property, Convert.ToBoolean(value));
    }

    public object? Deserialize(object value, Type type) {
        return Convert.ChangeType(value, type);
    }
}