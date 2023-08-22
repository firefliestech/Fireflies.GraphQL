public class GraphQLIntConverter : JsonConverter<GraphQLInt> {
    public override GraphQLInt Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        return reader.GetInt64();
    }

    public override void Write(Utf8JsonWriter writer, GraphQLInt value, JsonSerializerOptions options) {
        writer.WriteNumberValue(value);
    }
}