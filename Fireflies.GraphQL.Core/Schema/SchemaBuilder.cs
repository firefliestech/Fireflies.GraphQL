using System.Reflection;
using Fireflies.GraphQL.Abstractions;
using Fireflies.GraphQL.Core.Extensions;

namespace Fireflies.GraphQL.Core.Schema;

internal class SchemaBuilder {
    private readonly GraphQLOptions _options;

    // ReSharper disable once UnusedMember.Global
    public SchemaBuilder(GraphQLOptions options) {
        _options = options;
    }

    public __Schema GenerateSchema() {
        var topLevelTypes = _options.AllOperations
            .Select(x => x.Type)
            .SelectMany(qt =>
                qt.GetAllGraphQLMethods()
                    .SelectMany(x => new[] { x.DiscardTaskFromReturnType() }.Union(x.GetParameters().Select(y => y.ParameterType)))).Distinct();

        var allTypes = new HashSet<Type>();
        foreach(var type in topLevelTypes)
            FindAllTypes(allTypes, type);

        __Type? queryType = null;
        if(_options.QueryOperations.Any()) {
            queryType = new __Type(null, CreateQueryFields(_options.QueryOperations)) {
                Name = "Query",
                Kind = __TypeKind.OBJECT
            };
        }

        __Type? mutationType = null;
        if(_options.MutationsOperations.Any()) {
            mutationType = new __Type(null, CreateQueryFields(_options.MutationsOperations)) {
                Name = "Mutation",
                Kind = __TypeKind.OBJECT,
            };
        }

        __Type? subscriptionType = null;
        if(_options.SubscriptionOperations.Any()) {
            subscriptionType = new __Type(null, CreateQueryFields(_options.SubscriptionOperations)) {
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
            Description = _options.SchemaDescription,
            QueryType = queryType,
            MutationType = mutationType,
            SubscriptionType = subscriptionType,
            Directives = Array.Empty<__Directive>(),
            Types = allTypesIncludingRootTypes.ToArray()
        };

        return schema;
    }

    private void FindAllTypes(HashSet<Type> types, Type startingObject) {
        if(startingObject.IsEnumerable(out var elementType)) {
            FindAllTypes(types, elementType);
            return;
        }

        var underlyingType = Nullable.GetUnderlyingType(startingObject);
        if(underlyingType != null)
            startingObject = underlyingType;

        if(!types.Add(startingObject))
            return;

        if(Type.GetTypeCode(startingObject) != TypeCode.Object)
            return;

        if(startingObject.IsInterface) {
            foreach(var impl in startingObject.GetAllClassesThatImplements())
                FindAllTypes(types, impl);
        } else {
            foreach(var interf in startingObject.GetInterfaces()) {
                FindAllTypes(types, interf);
                foreach(var impl in interf.GetAllClassesThatImplements())
                    FindAllTypes(types, impl);
            }
        }

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
            foreach(var implementingType in startingObject.GetAllClassesThatImplements()) {
                types.Add(implementingType);
            }
        }
    }

    private IEnumerable<__Field> CreateQueryFields(IEnumerable<OperationDescriptor> operations) {
        var fields = new List<__Field>();

        foreach(var query in operations.Select(x => x.Method)) {
            fields.Add(new __Field(query) {
                Type = CreateType(query.DiscardTaskFromReturnType(), true),
                Args = GetArguments(query).ToArray()
            });
        }

        return fields;
    }

    private __Type CreateType(Type type, bool isTypeReference) {
        var baseType = GetBaseType(type);
        if(baseType.IsEnum) {
            if(type.IsEnumerable(out _)) {
                return new __Type(baseType) {
                    Kind = __TypeKind.LIST,
                    OfType = WrapNullable(Nullable.GetUnderlyingType(baseType) != null, CreateType(baseType, true))
                };
            }

            return new __Type(baseType, null, isTypeReference ? Array.Empty<__EnumValue>() : CreateEnumValues(baseType)) {
                Name = type.GraphQLName(),
                Kind = __TypeKind.ENUM,
            };
        }

        if(type.IsEnumerable()) {
            return new __Type(type) {
                Kind = __TypeKind.LIST,
                OfType = WrapNullable(Nullable.GetUnderlyingType(baseType) != null, CreateType(baseType, true))
            };
        }

        if(baseType == typeof(int)) {
            return new __Type(type) {
                Name = "Int",
                Kind = __TypeKind.SCALAR,
            };
        }

        if(baseType == typeof(string)) {
            return new __Type(type) {
                Name = "String",
                Kind = __TypeKind.SCALAR
            };
        }

        if(baseType == typeof(bool)) {
            return new __Type(type) {
                Name = "Boolean",
                Kind = __TypeKind.SCALAR
            };
        }

        if(baseType == typeof(decimal)) {
            return new __Type(type) {
                Name = "Float",
                Kind = __TypeKind.SCALAR,
            };
        }

        // Todo: ID is missing

        var isInputType = baseType.IsAssignableTo(typeof(GraphQLInput));

        if(!isInputType) {
            var fields = new List<__Field>();
            if(!isTypeReference) {
                fields.AddRange(GetFields(baseType));
            }

            if(type.IsInterface) {
                var interfaceType = new __Type(type, fields) {
                    Name = baseType.GraphQLName(),
                    Kind = type.HasCustomAttribute<GraphQLUnionAttribute>() ? __TypeKind.UNION : __TypeKind.INTERFACE,
                    Description = baseType.GetDescription()
                };

                if(!isTypeReference)
                    interfaceType.PossibleTypes = baseType.GetAllClassesThatImplements().Select(x => CreateType(x, true)).ToArray();

                return interfaceType;
            } else {
                var objectType = new __Type(type, fields) {
                    Name = baseType.GraphQLName(),
                    Kind = __TypeKind.OBJECT,
                    Description = baseType.GetDescription()
                };

                if(!isTypeReference)
                    objectType.Interfaces = baseType.GetInterfaces().Select(x => CreateType(x, true)).Where(x => x.Kind == __TypeKind.INTERFACE).ToArray();

                return objectType;
            }
        } else {
            var inputValues = new List<__InputValue>();

            foreach(var property in baseType.GetAllGraphQLProperties()) {
                inputValues.Add(new __InputValue {
                    Name = property.GraphQLName(),
                    Type = WrapNullable(NullabilityChecker.IsNullable(property), CreateType(property.PropertyType, true)),
                    Description = property.GetDescription()
                });
            }

            return new __Type(baseType, inputValues: inputValues) {
                Name = baseType.GraphQLName(),
                Kind = __TypeKind.INPUT_OBJECT,
                Description = baseType.GetDescription()
            };
        }
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
            var propertyType = property.PropertyType;
            var isNullable = NullabilityChecker.IsNullable(property);
            var underlyingType = Nullable.GetUnderlyingType(propertyType);
            if (underlyingType != null)
                propertyType = underlyingType;

            fields.Add(new __Field(property) {
                Name = property.GraphQLName(),
                Type = WrapNullable(isNullable, CreateType(propertyType, true))
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
                DefaultValue = GetDefaultValue(parameter),
                Description = parameter.GetDescription()
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

        return new __Type(null) {
            Kind = __TypeKind.NON_NULL,
            OfType = typeToBeWrapped
        };
    }

    private Type GetBaseType(Type type) {
        type = type.GetGraphQLType();

        var underlyingType = Nullable.GetUnderlyingType(type);
        if(underlyingType != null)
            type = underlyingType;

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
        return Enum.GetNames(type).Select(x => {
            var fieldInfo = type.GetField(x)!;
            var deprecationReason = fieldInfo.GetDeprecatedReason();
            var description = fieldInfo.GetDescription();
            return new __EnumValue { Name = x, Description = description, DeprecationReason = deprecationReason };
        }).ToArray();
    }
}