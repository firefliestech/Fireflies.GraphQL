namespace Fireflies.GraphQL.Core.Json;

public class ErrorJsonWriter : JsonWriter {
    protected override void Start() {
        Writer.WriteStartArray("errors");
    }

    protected override void Stop() {
        Writer.WriteEndArray();
    }
}