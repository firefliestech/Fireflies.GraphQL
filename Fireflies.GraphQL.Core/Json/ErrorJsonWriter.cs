using Fireflies.GraphQL.Core.Scalar;

namespace Fireflies.GraphQL.Core.Json;

public class ErrorJsonWriter : JsonWriter {
    public ErrorJsonWriter(ScalarRegistry scalarRegistry) : base(scalarRegistry) {
    }

    protected override void Start() {
        Writer.WriteStartArray("errors");
    }

    protected override void Stop() {
        Writer.WriteEndArray();
    }
}