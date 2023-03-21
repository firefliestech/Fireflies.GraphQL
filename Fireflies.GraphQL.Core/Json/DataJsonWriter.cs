using Fireflies.GraphQL.Core.Scalar;

namespace Fireflies.GraphQL.Core.Json;

public class DataJsonWriter : JsonWriter {
    public DataJsonWriter(ScalarRegistry scalarRegistry) : base(scalarRegistry) {
    }

    protected override void Start() {
        Writer.WriteStartObject("data"); // Data property
    }

    protected override void Stop() {
        Writer.WriteEndObject();
    }
}