namespace Fireflies.GraphQL.Core.Json;

public class DataJsonWriter : JsonWriter {
    protected override void Start() {
        Writer.WriteStartObject("data"); // Data property
    }

    protected override void Stop() {
        Writer.WriteEndObject();
    }
}