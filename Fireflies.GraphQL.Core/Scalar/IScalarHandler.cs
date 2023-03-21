using System.Text.Json;

namespace Fireflies.GraphQL.Core.Scalar;

public interface IScalarHandler {
    void Serialize(Utf8JsonWriter writer, object value);
    void Serialize(Utf8JsonWriter writer, string property, object value);
}