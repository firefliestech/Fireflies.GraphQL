using System.Text.Json;

namespace Fireflies.GraphQL.Core.Scalar;

public class UIntScalarHandler : IScalarHandler {
    public Type BaseType => typeof(int);

    public void Serialize(Utf8JsonWriter writer, object value) {
        writer.WriteNumberValue((ulong)Convert.ChangeType(value, TypeCode.UInt64));
    }

    public void Serialize(Utf8JsonWriter writer, string property, object value) {
        writer.WriteNumber(property, (ulong)Convert.ChangeType(value, TypeCode.UInt64));
    }

    public object? Deserialize(object value, Type type) {
        if(value.GetType().IsAssignableTo(type))
            return value;

        return Convert.ChangeType(value, type);
    }
}