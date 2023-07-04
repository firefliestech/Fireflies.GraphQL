using System.Text.Json;

namespace Fireflies.GraphQL.Core.Scalar;

public class IntScalarHandler : IScalarHandler {
    public Type BaseType => typeof(int);

    public void Serialize(Utf8JsonWriter writer, object value) {
        writer.WriteNumberValue((int)Convert.ChangeType(value, TypeCode.Int32));
    }

    public void Serialize(Utf8JsonWriter writer, string property, object value) {
        writer.WriteNumber(property, (int)Convert.ChangeType(value, TypeCode.Int32));
    }

    public object? Deserialize(object? value, Type type) {
        return Convert.ChangeType(value, type);
    }
}