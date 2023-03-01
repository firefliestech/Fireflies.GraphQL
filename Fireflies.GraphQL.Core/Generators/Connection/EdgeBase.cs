using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Fireflies.GraphQL.Abstractions;

namespace Fireflies.GraphQL.Core.Generators.Connection;

public abstract class EdgeBase<TBase> {
    // ReSharper disable once StaticMemberInGenericType
    private static readonly IEnumerable<PropertyInfo> IdProperties;

    static EdgeBase() {
        IdProperties = typeof(TBase).GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy).Where(x => x.GetCustomAttribute<GraphQlIdAttribute>(true) != null);
    }

    protected EdgeBase(TBase node) {
        Node = node;

        var cursor = new JsonObject();
        foreach(var property in IdProperties) {
            cursor.Add(property.Name, JsonValue.Create(property.GetValue(node)));
        }

        var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(cursor));
        Cursor = Convert.ToBase64String(plainTextBytes);
    }

    public TBase Node { get; set; }
    public string Cursor { get; }
}