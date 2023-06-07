using Fireflies.GraphQL.Core.Scalar;

namespace Fireflies.GraphQL.Core.Json;

public class JsonWriterFactory {
    private readonly ScalarRegistry _scalarRegistry;

    public JsonWriterFactory(ScalarRegistry scalarRegistry) {
        _scalarRegistry = scalarRegistry;
    }

    public ResultJsonWriter CreateResultWriter() {
        return new ResultJsonWriter(_scalarRegistry);
    }
}