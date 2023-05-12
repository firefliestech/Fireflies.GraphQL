using System.Text.Json;

namespace Fireflies.GraphQL.Core.Scalar;

public class VersionScalarHandler : IScalarHandler {
    public void Serialize(Utf8JsonWriter writer, object value) {
        writer.WriteStringValue(((Version)value).ToString());
    }

    public void Serialize(Utf8JsonWriter writer, string property, object value) {
        writer.WriteString(property, ((Version)value).ToString());
    }
}