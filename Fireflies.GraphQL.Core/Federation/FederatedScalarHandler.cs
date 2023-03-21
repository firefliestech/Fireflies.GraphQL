using System.Text.Json;
using Fireflies.GraphQL.Core.Scalar;

namespace Fireflies.GraphQL.Core.Federation;

public class FederatedScalarHandler : IScalarHandler {
    public void Serialize(Utf8JsonWriter writer, object? value) {
        var scalar = value as GraphQLScalar;
        if(scalar?.Value == null)
            writer.WriteNullValue();
        else
            writer.WriteStringValue(scalar.Value);
    }

    public void Serialize(Utf8JsonWriter writer, string property, object? value) {
        var scalar = value as GraphQLScalar;
        if(scalar?.Value == null)
            writer.WriteNull(property);
        else
            writer.WriteString(property, scalar.Value);
    }
}