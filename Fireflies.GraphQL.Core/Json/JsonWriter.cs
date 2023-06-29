using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using Fireflies.GraphQL.Abstractions;
using Fireflies.GraphQL.Core.Scalar;

namespace Fireflies.GraphQL.Core.Json;

public class JsonWriter : IErrorCollection {
    private readonly ScalarRegistry _scalarRegistry;

    protected List<object> Errors;

    private readonly MemoryStream _stream;
    protected readonly Utf8JsonWriter _writer;

    public JsonWriterMetadata Metadata { get; }

    public IGraphQLError AddError(string code, string message) {
        var error = new GraphQLError(code, message);
        Errors.Add(error);
        return error;
    }

    public IGraphQLError AddError(IGraphQLError error) {
        Errors.Add(error);
        return error;
    }

    public IGraphQLError AddError(IGraphQLPath path, string code, string message) {
        var error = new GraphQLError(path, code, message);
        Errors.Add(error);
        return error;
    }

    public void AddError(JsonNode node) {
        Errors.Add(new GraphQLRawError(node));
    }

    public JsonWriter(ScalarRegistry scalarRegistry, JsonWriter? parent = null) {
        _scalarRegistry = scalarRegistry;
        _stream = new MemoryStream();
        _writer = new Utf8JsonWriter(_stream, new JsonWriterOptions { SkipValidation = true });

        Metadata = parent?.Metadata ?? new JsonWriterMetadata();
        Errors = parent == null ? new List<object>() : parent.Errors;
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

    public virtual void WriteValue(object value, Type elementType) {
        if(elementType.IsEnum) {
            _writer.WriteStringValue(value.ToString());
            return;
        }

        if(_scalarRegistry.GetHandler(value.GetType(), out var handler)) {
            handler!.Serialize(_writer, value);
        }

        throw new ArgumentOutOfRangeException($"No scalar handler found for {elementType.Name}");
    }

    public virtual void WriteValue(string property, object value, Type elementType) {
        if(elementType.IsEnum) {
            _writer.WriteString(property, value.ToString());
            return;
        }

        var memberInfo = value.GetType();

        if(memberInfo.IsSubclassOf(typeof(GraphQLId))) {
            _writer.WriteString(property, value.ToString());
            return;
        }

        if(_scalarRegistry.GetHandler(memberInfo, out var handler)) {
            handler!.Serialize(_writer, property, value);
            return;
        }

        throw new ArgumentOutOfRangeException($"No scalar handler found for {elementType.Name}");
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