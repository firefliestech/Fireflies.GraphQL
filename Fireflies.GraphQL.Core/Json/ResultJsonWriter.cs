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

    public void WriteStartObject() {
        EnsureData();

        base.WriteStartObject();
    }

    public void WriteStartObject(string fieldName) {
        EnsureData();

        base.WriteStartObject(fieldName);
    }

    public void WriteEndObject() {
        EnsureData();

        base.WriteEndObject();
    }

    public void WriteEndArray() {
        EnsureData();

        base.WriteEndArray();
    }

    public void WriteNull(string fieldName) {
        EnsureData();

        base.WriteNull(fieldName);
    }

    public void WriteValue(object value, TypeCode typeCode, Type elementType) {
        EnsureData();

        base.WriteValue(value, typeCode, elementType);
    }

    public void WriteValue(string property, object value, TypeCode typeCode, Type elementType) {
        EnsureData();
        base.WriteValue(property, value, typeCode, elementType);
    }

    private void EnsureData() {
        if(!_empty)
            return;

        _writer.WriteStartObject("data");
        _empty = false;
    }
}