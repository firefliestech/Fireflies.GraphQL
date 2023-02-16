using System.Reflection;
using Fireflies.GraphQL.Contract;
using Fireflies.GraphQL.Core.Extensions;

namespace Fireflies.GraphQL.Core.Schema;

// ReSharper disable once InconsistentNaming
public class __SchemaQuery {
    private readonly GraphQLOptions _options;

    public __SchemaQuery(GraphQLOptions options) {
        _options = options;
    }

    // ReSharper disable once UnusedMember.Global
    [GraphQLQuery]
    public Task<__Schema> __Schema() {
        var topLevelTypes = _options.AllOperations
            .Select(x => x.Type)
            .SelectMany(qt =>
                qt.GetAllGraphQLMethods()
                    .SelectMany(x => new[] { x.ReturnType.GetGenericArguments()[0] }.Union(x.GetParameters().Select(y => y.ParameterType)))).Distinct();

        var allTypes = new HashSet<Type>();
        foreach(var type in topLevelTypes)
            FindAllTypes(allTypes, type);

        __Type? queryType = null;
        if(_options.QueryOperations.Any()) {
            queryType = new __Type(CreateQueryFields(_options.QueryOperations)) {
                Name = "Query",
                Kind = __TypeKind.OBJECT
            };
        }

        __Type? mutationType = null;
        if(_options.MutationsOperations.Any()) {
            mutationType = new __Type(CreateQueryFields(_options.MutationsOperations)) {
                Name = "Mutation",
                Kind = __TypeKind.OBJECT,
            };
        }

        __Type? subscriptionType = null;
        if(_options.SubscriptionOperations.Any()) {
            subscriptionType = new __Type(CreateQueryFields(_options.SubscriptionOperations)) {
                Name = "Subscription",
                Kind = __TypeKind.OBJECT
            };
        }

        var allTypesIncludingRootTypes = allTypes.Select(t => CreateType(t, false));
        if(queryType != null)
            allTypesIncludingRootTypes = allTypesIncludingRootTypes.Union(new[] { queryType });
        if(mutationType != null)
            allTypesIncludingRootTypes = allTypesIncludingRootTypes.Union(new[] { mutationType });
        if(subscriptionType != null)
            allTypesIncludingRootTypes = allTypesIncludingRootTypes.Union(new[] { subscriptionType });

        var schema = new __Schema {
            QueryType = queryType,
            MutationType = mutationType,
            SubscriptionType = subscriptionType,
            Directives = Array.Empty<__Directive>(),
            Types = allTypesIncludingRootTypes.ToArray()
        };

        return Task.FromResult(schema);
    }

    private void FindAllTypes(HashSet<Type> types, Type startingObject) {
        if(startingObject.IsEnumerable(out var elementType)) {
            FindAllTypes(types, elementType);
            return;
        }

        if(!types.Add(startingObject))
            return;

        if(Type.GetTypeCode(startingObject) != TypeCode.Object)
            return;

        foreach(var method in startingObject.GetAllGraphQLMethods()) {
            foreach(var parameter in method.GetParameters()) {
                FindAllTypes(types, parameter.ParameterType);
            }

            FindAllTypes(types, method.ReturnType.GetGraphQLType());
        }

        foreach(var property in startingObject.GetAllGraphQLProperties()) {
            FindAllTypes(types, property.PropertyType);
        }

        if(startingObject.IsInterface) {
            foreach(var implementingType in GetAllClassesThatImplements(startingObject)) {
                types.Add(implementingType);
            }
        }
    }

    private IEnumerable<__Field> CreateQueryFields(IEnumerable<OperationDescriptor> operations) {
        var fields = new List<__Field>();

        foreach(var query in operations.Select(x => x.Method)) {
            fields.Add(new __Field(query) {
                Type = CreateType(query.ReturnType.GetGenericArguments()[0], true),
                Args = GetArguments(query).ToArray()
            });
        }

        return fields;
    }

    private __Type CreateType(Type type, bool isTypeReference) {
        var baseType = GetBaseType(type);
        if(baseType.IsEnum) {
            if(type.IsEnumerable(out _)) {
                return new __Type {
                    Kind = __TypeKind.LIST,
                    OfType = WrapNullable(Nullable.GetUnderlyingType(baseType) != null, CreateType(baseType, true))
                };
            } else {
                return new __Type(null, isTypeReference ? Array.Empty<__EnumValue>() : CreateEnumValues(baseType)) {
                    Name = type.GraphQLName(),
                    Kind = __TypeKind.ENUM,
                };
            }
        }

        if(baseType == typeof(int)) {
            return new __Type {
                Name = "Int",
                Kind = __TypeKind.SCALAR,
            };
        }

        if(baseType == typeof(string)) {
            return new __Type {
                Name = "String",
                Kind = __TypeKind.SCALAR
            };
        }

        if(baseType == typeof(bool)) {
            return new __Type {
                Name = "Boolean",
                Kind = __TypeKind.SCALAR
            };
        }

        if(baseType == typeof(decimal)) {
            return new __Type {
                Name = "Float",
                Kind = __TypeKind.SCALAR,
            };
        }

        // Todo: ID is missing

        if(type.IsEnumerable()) {
            return new __Type {
                Kind = __TypeKind.LIST,
                OfType = WrapNullable(Nullable.GetUnderlyingType(baseType) != null, CreateType(baseType, true))
            };
        }

        var isInputType = baseType.IsAssignableTo(typeof(GraphQLInput));

        if(!isInputType) {
            var fields = new List<__Field>();
            if(!isTypeReference) {
                fields.AddRange(GetFields(baseType));
            }

            if(type.IsInterface) {
                return new __Type(fields) {
                    Name = baseType.GraphQLName(),
                    Kind = __TypeKind.INTERFACE,
                    PossibleTypes = GetAllClassesThatImplements(baseType).Select(x => CreateType(x, false)).ToArray()
                };
            } else {
                return new __Type(fields) {
                    Name = baseType.GraphQLName(),
                    Kind = __TypeKind.OBJECT,
                };
            }
        } else {
            var inputValues = new List<__InputValue>();

            foreach(var property in baseType.GetAllGraphQLProperties()) {
                inputValues.Add(new __InputValue {
                    Name = property.GraphQLName(),
                    Type = WrapNullable(NullabilityChecker.IsNullable(property),
                        CreateType(property.PropertyType, true))
                });
            }

            return new __Type(inputValues: inputValues) {
                Name = baseType.GraphQLName(),
                Kind = __TypeKind.INPUT_OBJECT
            };
        }
    }

    private IEnumerable<Type> GetAllClassesThatImplements(Type baseType) {
        return AppDomain.CurrentDomain
            .GetAssemblies()
            .SelectMany(x => x.GetTypes())
            .Where(x => x.IsClass && x.IsAssignableTo(baseType));
    }

    private List<__Field> GetFields(Type baseType) {
        var fields = new List<__Field>();

        foreach(var method in baseType.GetAllGraphQLMethods()) {
            fields.Add(new __Field(method) {
                Type = CreateType(method.ReturnType, true),
                Args = GetArguments(method).ToArray(),
            });
        }

        foreach(var property in baseType.GetAllGraphQLProperties()) {
            fields.Add(new __Field(property) {
                Name = property.GraphQLName(),
                Type = WrapNullable(NullabilityChecker.IsNullable(property),
                    CreateType(property.PropertyType, true))
            });
        }

        return fields;
    }

    private List<__InputValue> GetArguments(MethodInfo method) {
        var args = new List<__InputValue>();
        foreach(var parameter in method.GetParameters()) {
            args.Add(new __InputValue {
                Name = parameter.GraphQLName(),
                Type = CreateType(parameter.ParameterType, true),
                DefaultValue = GetDefaultValue(parameter)
            });
        }

        return args;
    }

    private static string? GetDefaultValue(ParameterInfo parameter) {
        var defaultValue = parameter.RawDefaultValue;
        if(defaultValue == null)
            return null;

        if(parameter.ParameterType == typeof(bool)) {
            return defaultValue.ToString()?.ToLower();
        }

        return defaultValue.ToString();
    }

    private __Type WrapNullable(bool nullable, __Type typeToBeWrapped) {
        if(nullable) {
            return typeToBeWrapped;
        }

        return new __Type {
            Kind = __TypeKind.NON_NULL,
            OfType = typeToBeWrapped
        };
    }

    private Type GetBaseType(Type type) {
        type = type.GetGraphQLType();

        if(type.IsEnum)
            return type;

        switch(Type.GetTypeCode(type)) {
            case TypeCode.Int16:
            case TypeCode.Int32:
            case TypeCode.Int64:
            case TypeCode.UInt16:
            case TypeCode.UInt32:
            case TypeCode.UInt64:
            case TypeCode.Byte:
            case TypeCode.SByte:
                return typeof(int);

            case TypeCode.Boolean:
                return typeof(bool);

            case TypeCode.Char:
            case TypeCode.String:
                return typeof(string);

            case TypeCode.Single:
            case TypeCode.Double:
            case TypeCode.Decimal:
                return typeof(decimal);

            default:
                return type;
        }
    }

    private __EnumValue[] CreateEnumValues(Type type) {
        return Enum.GetNames(type).Select(x => new __EnumValue { Name = x }).ToArray();
    }
}