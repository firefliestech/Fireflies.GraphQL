using System.Text.Json;

namespace Fireflies.GraphQL.Core.Scalar;

public interface IScalarHandler {
    Type BaseType { get; }
    void Serialize(Utf8JsonWriter writer, object value);
    void Serialize(Utf8JsonWriter writer, string property, object value);
}