using Fireflies.GraphQL.Core.Scalar;

namespace Fireflies.GraphQL.Core.Json;

public class JsonWriterFactory {
    private readonly ScalarRegistry _scalarRegistry;

    public JsonWriterFactory(ScalarRegistry scalarRegistry) {
        _scalarRegistry = scalarRegistry;
    }

    public JsonWriter CreateWriter() {
        return new JsonWriter(_scalarRegistry);
    }

    public ResultJsonWriter CreateResultWriter() {
        return new ResultJsonWriter(_scalarRegistry);
    }
}