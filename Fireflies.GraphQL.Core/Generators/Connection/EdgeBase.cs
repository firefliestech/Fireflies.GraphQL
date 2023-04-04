using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Fireflies.GraphQL.Abstractions;
using Fireflies.Utility.Reflection;
using Fireflies.Utility.Reflection.Fasterflect;

namespace Fireflies.GraphQL.Core.Generators.Connection;

public abstract class EdgeBase<TBase> {
    protected EdgeBase(TBase node) {
        Node = node;

        var cursor = new JsonObject();
        foreach(var member in ReflectionCache.GetMembers(node.GetType())) {
            if(member.HasCustomAttribute<GraphQLIdAttribute>()) {
                var value = member switch {
                    PropertyInfo propertyInfo => Reflect.PropertyGetter(propertyInfo),
                    MethodInfo methodInfo => Reflect.Method(methodInfo, typeof(WrapperRegistry))(node!, (WrapperRegistry)null!), //TODO: Maybe ID-properties should not be wrapped as a method?
                    _ => null
                };

                if(value == null) {
                    cursor.Add(member.Name, JsonValue.Create<object>(null));
                } else {
                    cursor.Add(member.Name, JsonValue.Create(value.ToString()));
                }

            }
        }

        var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(cursor));
        Cursor = Convert.ToBase64String(plainTextBytes);
    }

    public TBase Node { get; set; }
    public string Cursor { get; }
}