using System.Text.Json;

namespace Fireflies.GraphQL.Core.Scalar;

public class FloatScalarHandler : IScalarHandler {
    public Type BaseType => typeof(decimal);

    public void Serialize(Utf8JsonWriter writer, object value) {
        writer.WriteNumberValue((decimal)Convert.ChangeType(value, TypeCode.Decimal));
    }

    public void Serialize(Utf8JsonWriter writer, string property, object value) {
        writer.WriteNumber(property, (decimal)Convert.ChangeType(value, TypeCode.Decimal));
    }
}