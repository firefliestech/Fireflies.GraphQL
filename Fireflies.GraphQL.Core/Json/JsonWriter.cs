using System.Text.Json;
using System.Text.Json.Nodes;
using Fireflies.GraphQL.Abstractions;
using Fireflies.GraphQL.Core.Scalar;

namespace Fireflies.GraphQL.Core.Json;

public class JsonWriter {
    private readonly ScalarRegistry _scalarRegistry;
    private readonly MemoryStream _stream;
    private readonly Utf8JsonWriter _writer;
    private List<(string Message, string Code)>? _errors;
    private bool _empty = true;

    public JsonWriter(ScalarRegistry scalarRegistry) {
        _scalarRegistry = scalarRegistry;
        _stream = new MemoryStream();
        _writer = new Utf8JsonWriter(_stream);
        _writer.WriteStartObject(); // Root object
    }
    
    public void AddError(string message, string code) {
        _errors ??= new();
        _errors.Add((message, code));
    }

    public async Task<byte[]> GetBuffer() {
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
        await _writer.FlushAsync();

        var result = _stream.ToArray();
        await _writer.DisposeAsync();
        await _stream.DisposeAsync();

        return result;
    }

    public void WriteStartArray(string fieldName) {
        EnsureData();
        _writer.WriteStartArray(fieldName);
    }

    public void WriteStartObject() {
        EnsureData();

        _writer.WriteStartObject();
    }

    public void WriteStartObject(string fieldName) {
        EnsureData();

        _writer.WriteStartObject(fieldName);
    }

    public void WriteEndObject() {
        EnsureData();

        _writer.WriteEndObject();
    }

    public void WriteEndArray() {
        EnsureData();

        _writer.WriteEndArray();
    }

    public void WriteNull(string fieldName) {
        EnsureData();

        _writer.WriteNull(fieldName);
    }

    public void WriteValue(object value, TypeCode typeCode, Type elementType) {
        EnsureData();

        if(elementType.IsEnum) {
            _writer.WriteStringValue(value.ToString());
            return;
        }

        switch(typeCode) {
            case TypeCode.Int16:
            case TypeCode.Int32:
            case TypeCode.Int64:
            case TypeCode.UInt16:
            case TypeCode.UInt32:
            case TypeCode.UInt64:
            case TypeCode.Byte:
            case TypeCode.SByte:
                _writer.WriteNumberValue((int)Convert.ChangeType(value, TypeCode.Int32));
                break;

            case TypeCode.Boolean:
                _writer.WriteBooleanValue((bool)value);
                break;

            case TypeCode.Char:
            case TypeCode.String:
                _writer.WriteStringValue((string)value);
                break;

            case TypeCode.Single:
            case TypeCode.Double:
            case TypeCode.Decimal:
                _writer.WriteNumberValue((decimal)Convert.ChangeType(value, TypeCode.Decimal));
                break;

            default:
                if(_scalarRegistry.GetHandler(value.GetType(), out var handler)) {
                    handler!.Serialize(_writer, value);
                }

                throw new ArgumentOutOfRangeException(nameof(typeCode));
        }
    }

    public void WriteValue(string property, object value, TypeCode typeCode, Type elementType) {
        EnsureData();

        if(elementType.IsEnum) {
            _writer.WriteString(property, value.ToString());
            return;
        }

        switch(typeCode) {
            case TypeCode.Int16:
            case TypeCode.Int32:
            case TypeCode.Int64:
            case TypeCode.UInt16:
            case TypeCode.UInt32:
            case TypeCode.UInt64:
            case TypeCode.Byte:
            case TypeCode.SByte:
                _writer.WriteNumber(property, (int)Convert.ChangeType(value, TypeCode.Int32));
                break;

            case TypeCode.Boolean:
                _writer.WriteBoolean(property, (bool)value);
                break;

            case TypeCode.Char:
            case TypeCode.String:
                _writer.WriteString(property, (string)value);
                break;

            case TypeCode.Single:
            case TypeCode.Double:
            case TypeCode.Decimal:
                _writer.WriteNumber(property, (decimal)Convert.ChangeType(value, TypeCode.Decimal));
                break;

            default:
                var memberInfo = value.GetType();

                if(memberInfo.IsSubclassOf(typeof(GraphQLId))) {
                    _writer.WriteString(property, value.ToString());
                    break;
                }

                if(_scalarRegistry.GetHandler(memberInfo, out var handler)) {
                    handler!.Serialize(_writer, property, value);
                    break;
                }

                throw new ArgumentOutOfRangeException(nameof(typeCode));
        }
    }

    private void EnsureData() {
        if(_empty) {
            _writer.WriteStartObject("data");
            _empty = false;
        }
    }

    public void WriteRaw(string fieldName, JsonNode jsonObject) {
        _writer.WritePropertyName(fieldName);
        jsonObject.WriteTo(_writer);
    }
}