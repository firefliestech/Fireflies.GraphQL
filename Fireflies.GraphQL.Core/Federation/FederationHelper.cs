using System.Reflection;
using System.Reflection.Emit;
using Fireflies.GraphQL.Core.Extensions;
using GraphQLParser.AST;
using GraphQLParser.Visitors;
using Newtonsoft.Json.Linq;

namespace Fireflies.GraphQL.Core.Federation;

public static class FederationHelper {
    private static readonly MethodInfo EnumerableMethod;
    private static readonly HttpClient HttpClient = new();

    static FederationHelper() {
        EnumerableMethod = typeof(FederationHelper).GetMethod(nameof(GetEnumerableResult), BindingFlags.NonPublic | BindingFlags.Static)!;
    }

    public static async Task<T?> ExecuteRequest<T>(ASTNode astNode, GraphQLContext context, string url) {
        var typeNameAdder = new TypeNameAdder();
        await typeNameAdder.VisitAsync(astNode, context);

        var writer = new StringWriter();
        await new SDLPrinter().PrintAsync(astNode, writer, context.CancellationToken);

        var result = await HttpClient.PostAsync(url, new StringContent(FederationQueryBuilder.BuildQuery(writer.ToString(), OperationType.Query, "")));
        var jObject = JObject.Parse(await result.Content.ReadAsStringAsync());

        var field = (GraphQLField)astNode;

        return GetResult<T>(jObject["data"]?[field.Name.StringValue]);
    }

    public static T? GetField<T>(JObject? data, string field) {
        if(data == null)
            return default;

        return GetResult<T>(data[field]);
    }

    private static T? GetResult<T>(JToken? token) {
        if(token == null)
            return default;

        if(typeof(T).IsEnumerable(out var elementType)) {
            return (T)EnumerableMethod.MakeGenericMethod(elementType).Invoke(null, new object[] { token });
        }

        return CreateInstance<T>(token);
    }

    private static IEnumerable<T> GetEnumerableResult<T>(JToken token) {
        return token.Select(CreateInstance<T>);
    }

    private static T CreateInstance<T>(JToken token) {
        switch(Type.GetTypeCode(typeof(T))) {
            case TypeCode.Int16:
            case TypeCode.Int32:
            case TypeCode.Int64:
            case TypeCode.UInt16:
            case TypeCode.UInt32:
            case TypeCode.UInt64:
            case TypeCode.Byte:
            case TypeCode.SByte:
                return token.Value<T>();

            case TypeCode.Boolean:
                return token.Value<T>();

            case TypeCode.Char:
            case TypeCode.String:
                return token.Value<T>();

            case TypeCode.Single:
            case TypeCode.Double:
            case TypeCode.Decimal:
                return token.Value<T>();

            default:
                var typename = token.Value<string>("__typename");
                if(typename != null) {
                    var assembly = typeof(T).Assembly;
                    var implementation = assembly.GetType(typename)!;
                    return (T)Activator.CreateInstance(implementation, token)!;
                }

                return (T)Activator.CreateInstance(typeof(T), token)!;
        }
    }

    private class TypeNameAdder : ASTVisitor<GraphQLContext> {
        protected override ValueTask VisitSelectionSetAsync(GraphQLSelectionSet selectionSet, GraphQLContext context) {
            if(!selectionSet.Selections.Any(x => x.Kind == ASTNodeKind.Field && ((GraphQLField)x).Name.StringValue == "__typename")) {
                selectionSet.Selections.Add(new GraphQLField {
                    Name = new GraphQLName("__typename")
                });
            }

            return base.VisitSelectionSetAsync(selectionSet, context);
        }
    }
}