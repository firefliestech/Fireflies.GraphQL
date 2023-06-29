using System.Text.Json;

namespace Fireflies.GraphQL.Core.Scalar;

public class StringScalarHandler : IScalarHandler {
    public Type BaseType => typeof(string);

    public void Serialize(Utf8JsonWriter writer, object value) {
        writer.WriteStringValue(value?.ToString() ?? "");
    }

    public void Serialize(Utf8JsonWriter writer, string property, object value) {
        writer.WriteString(property, value.ToString());
    }
}