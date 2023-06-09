using System.Text.Json.Nodes;
using Fireflies.GraphQL.Core.Scalar;

namespace Fireflies.GraphQL.Core.Json;

public class ResultJsonWriter : JsonWriter {
    private bool _empty = true;

    public ResultJsonWriter(ScalarRegistry scalarRegistry) : base(scalarRegistry) {
        _writer.WriteStartObject();
    }

    public override async Task<byte[]> GetBuffer() {
        if(!_empty)
            _writer.WriteEndObject();

        if(Metadata.Federated)
            _writer.WriteBoolean("_federated", true);

        if(Errors.Count > 0) {
            _writer.WriteStartArray("errors");
            foreach(var error in Errors) {
                if(error is GraphQLRawError raw) {
                    raw.Node.WriteTo(_writer);
                } else {
                    _writer.WriteStartObject();

                    WriteError(error);

                    _writer.WriteEndObject();
                }
            }

            _writer.WriteEndArray();
        }

        _writer.WriteEndObject();

        return await base.GetBuffer();
    }

    private void WriteError(IGraphQLError error) {
        if(error is not GraphQLError e)
            return;

        _writer.WriteString("message", e.Message);

        if(e.Path != null) {
            _writer.WriteStartArray("path");
            foreach(var path in e.Path.Path) {
                if(path is int i)
                    _writer.WriteNumberValue(i);
                else
                    _writer.WriteStringValue(path.ToString());
            }

            _writer.WriteEndArray();
        }

        _writer.WriteStartObject("extensions");

        foreach(var extension in e.Extensions) {
            _writer.WriteString(extension.Key, extension.Value);
        }

        _writer.WriteEndObject();
    }

    public override void WriteStartArray(string fieldName) {
        EnsureData();
        base.WriteStartArray(fieldName);
    }

    public override void WriteStartObject() {
        EnsureData();
        base.WriteStartObject();
    }

    public override void WriteStartObject(string fieldName) {
        EnsureData();
        base.WriteStartObject(fieldName);
    }

    public override void WriteEndObject() {
        EnsureData();
        base.WriteEndObject();
    }

    public override void WriteEndArray() {
        EnsureData();
        base.WriteEndArray();
    }

    public override void WriteNull(string fieldName) {
        EnsureData();
        base.WriteNull(fieldName);
    }

    public override void WriteValue(object value, TypeCode typeCode, Type elementType) {
        EnsureData();
        base.WriteValue(value, typeCode, elementType);
    }

    public override void WriteValue(string property, object value, TypeCode typeCode, Type elementType) {
        EnsureData();
        base.WriteValue(property, value, typeCode, elementType);
    }

    public override void WriteRaw(string fieldName, JsonNode jsonObject) {
        EnsureData();
        base.WriteRaw(fieldName, jsonObject);
    }

    private void EnsureData() {
        if(!_empty)
            return;

        _writer.WriteStartObject("data");
        _empty = false;
    }
}