using System.Reflection;
using Fireflies.GraphQL.Abstractions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Fireflies.GraphQL.Core.Generators.Connection;

public abstract class EdgeBase<TBase> {
    // ReSharper disable once StaticMemberInGenericType
    private static readonly IEnumerable<PropertyInfo> IdProperties;

    static EdgeBase() {
        IdProperties = typeof(TBase).GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy).Where(x => x.GetCustomAttribute<GraphQlIdAttribute>(true) != null);
    }

    protected EdgeBase(TBase node) {
        Node = node;

        var cursor = new JObject();
        foreach(var property in IdProperties) {
            cursor.Add(property.Name, new JValue(property.GetValue(node)));
        }

        var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(cursor));
        Cursor = Convert.ToBase64String(plainTextBytes);
    }

    public TBase Node { get; set; }
    public string Cursor { get; }
}