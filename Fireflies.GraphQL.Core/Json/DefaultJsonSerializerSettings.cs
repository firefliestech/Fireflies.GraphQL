using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fireflies.GraphQL.Core.Json;

public static class DefaultJsonSerializerSettings {
    public static JsonSerializerOptions DefaultSettings { get; } = new() { PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() }, DefaultIgnoreCondition = JsonIgnoreCondition.Never };
}