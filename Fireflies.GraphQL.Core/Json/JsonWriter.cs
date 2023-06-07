using System.Text.Json;
using System.Text.Json.Nodes;
using Fireflies.GraphQL.Abstractions;
using Fireflies.GraphQL.Core.Scalar;

namespace Fireflies.GraphQL.Core.Json;

public class JsonWriter {
    private readonly ScalarRegistry _scalarRegistry;
    private readonly MemoryStream _stream;
    protected readonly Utf8JsonWriter _writer;

    public JsonWriterMetadata Metadata { get; }

    public JsonWriter(ScalarRegistry scalarRegistry, JsonWriter? parent = null) {
        _scalarRegistry = scalarRegistry;
        _stream = new MemoryStream();
        _writer = new Utf8JsonWriter(_stream, new JsonWriterOptions { SkipValidation = true });

        Metadata = parent?.Metadata ?? new JsonWriterMetadata();
    }

    public virtual async Task<byte[]> GetBuffer() {
        await _writer.FlushAsync().ConfigureAwait(false);

        var result = _stream.ToArray();
        await _writer.DisposeAsync().ConfigureAwait(false);
        await _stream.DisposeAsync().ConfigureAwait(false);

        return result;
    }

    public virtual void WriteStartArray(string fieldName) {
        _writer.WriteStartArray(fieldName);
    }

    public virtual void WriteStartObject() {
        _writer.WriteStartObject();
    }

    public virtual void WriteStartObject(string fieldName) {
        _writer.WriteStartObject(fieldName);
    }

    public virtual void WriteEndObject() {
        _writer.WriteEndObject();
    }

    public virtual void WriteEndArray() {
        _writer.WriteEndArray();
    }

    public virtual void WriteNull(string fieldName) {
        _writer.WriteNull(fieldName);
    }

    public virtual void WriteValue(object value, TypeCode typeCode, Type elementType) {
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

    public virtual void WriteValue(string property, object value, TypeCode typeCode, Type elementType) {
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

    public virtual void WriteRaw(string fieldName, JsonNode jsonObject) {
        _writer.WritePropertyName(fieldName);
        jsonObject.WriteTo(_writer);
    }

    public async Task WriteRaw(JsonWriter otherWriter) {
        _writer.WriteRawValue(await otherWriter.GetBuffer());
    }

    public JsonWriter CreateSubWriter() {
        return new JsonWriter(_scalarRegistry, this);
    }
}