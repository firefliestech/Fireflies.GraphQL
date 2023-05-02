using System.Text.Json.Nodes;
using Fireflies.GraphQL.Core.Scalar;

namespace Fireflies.GraphQL.Core.Json;

public class ResultJsonWriter : JsonWriter {
    private List<(string Message, string Code)>? _errors;
    private bool _empty = true;

    public ResultJsonWriter(ScalarRegistry scalarRegistry) : base(scalarRegistry) {
        _writer.WriteStartObject();
    }

    public void AddError(string message, string code) {
        _errors ??= new();
        _errors.Add((message, code));
    }

    public override async Task<byte[]> GetBuffer() {
        if(!_empty)
            _writer.WriteEndObject();

        if(_errors != null) {
            _writer.WriteStartArray("errors");
            foreach(var error in _errors) {
                _writer.WriteStartObject();
                _writer.WriteString("message", error.Message);

                _writer.WriteStartObject("extensions");
                _writer.WriteString("code", error.Code);
                _writer.WriteEndObject();

                _writer.WriteEndObject();
            }

            _writer.WriteEndArray();
        }

        _writer.WriteEndObject();

        return await base.GetBuffer();
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