using System.Reflection;
using Fireflies.GraphQL.Abstractions;
using Fireflies.GraphQL.Abstractions.Schema;
using Fireflies.GraphQL.Core.Extensions;
using Fireflies.GraphQL.Core.Federation;
using Fireflies.GraphQL.Core.Federation.Schema;
using Fireflies.GraphQL.Core.Scalar;
using Fireflies.Utility.Reflection;
using GraphQLParser.AST;

namespace Fireflies.GraphQL.Core.Schema;

internal class SchemaBuilder {
    private readonly GraphQLOptions _options;
    private readonly WrapperRegistry _wrapperRegistry;
    private readonly ScalarRegistry _scalarRegistry;
    private readonly List<FederationSchema> _federationSchemas;
    private readonly HashSet<Type> _inputTypes = new();
    private readonly HashSet<Type> _ignore = new();
    private int _inputLevel;

    // ReSharper disable once UnusedMember.Global
    public SchemaBuilder(GraphQLOptions options, WrapperRegistry wrapperRegistry, ScalarRegistry scalarRegistry, List<FederationSchema> federationSchemas) {
        _options = options;
        _wrapperRegistry = wrapperRegistry;
        _scalarRegistry = scalarRegistry;
        _federationSchemas = federationSchemas;
        _ignore.Add(typeof(CancellationToken));
        _ignore.Add(typeof(ASTNode));
        _ignore.Add(typeof(__Schema));
    }

    public __Schema GenerateSchema() {
        var allTypes = new HashSet<Type>();
        foreach(var type in _options.AllOperations.Where(x => !x.Method.HasCustomAttribute<GraphQLFederatedAttribute>()))
            FindAllTypes(allTypes, type.Type, true);

        __Type? queryType = null;
        if(_options.QueryOperations.Any()) {
            queryType = new __Type(null, CreateOperationFields(_options.QueryOperations.Where(qo => !qo.Name.StartsWith("__")), s => s.QueryType?.Name)) {
                Name = "Query",
                Kind = __TypeKind.OBJECT
            };
        }

        __Type? mutationType = null;
        if(_options.MutationsOperations.Any()) {
            mutationType = new __Type(null, CreateOperationFields(_options.MutationsOperations, s => s.MutationType?.Name)) {
                Name = "Mutation",
                Kind = __TypeKind.OBJECT,
            };
        }

        __Type? subscriptionType = null;
        if(_options.SubscriptionOperations.Any()) {
            subscriptionType = new __Type(null, CreateOperationFields(_options.SubscriptionOperations, s => s.SubscriptionType?.Name)) {
                Name = "Subscription",
                Kind = __TypeKind.OBJECT
            };
        }

        var allTypesIncludingRootTypes = allTypes.Select(t => CreateType(t, false)).ToList();
        if(queryType != null)
            allTypesIncludingRootTypes.Add(queryType);
        if(mutationType != null)
            allTypesIncludingRootTypes.Add(mutationType);
        if(subscriptionType != null)
            allTypesIncludingRootTypes.Add(subscriptionType);

        foreach(var federationSchema in _federationSchemas) {
            foreach(var federatedType in federationSchema.Types) {
                if(federatedType.Name == federationSchema.QueryType?.Name || federatedType.Name == federationSchema.MutationType?.Name || federatedType.Name == federationSchema.SubscriptionType?.Name)
                    continue;

                if(allTypesIncludingRootTypes.Any(x => x.Name == federatedType.Name))
                    continue;

                allTypesIncludingRootTypes.Add(__Type.FromFederation(federatedType));
            }
        }

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

    private void FindAllTypes(HashSet<Type> types, Type startingObject, bool isOperation = false) {
        if(_ignore.Contains(startingObject))
            return;

        startingObject = _wrapperRegistry.GetWrapperOfSelf(startingObject);
        startingObject = startingObject.GetGraphQLBaseType();

        if(startingObject.IsCollection(out var elementType)) {
            FindAllTypes(types, elementType);
            return;
        }

        var underlyingType = Nullable.GetUnderlyingType(startingObject);
        if(underlyingType != null)
            startingObject = underlyingType;

        if(!isOperation && !types.Add(startingObject))
            return;

        if(!_scalarRegistry.IsValidGraphQLObjectType(startingObject))
            return;

        if(startingObject.IsInterface) {
            foreach(var impl in ReflectionCache.GetAllClassesThatImplements(startingObject))
                FindAllTypes(types, _wrapperRegistry.GetWrapperOfSelf(impl));
        } else {
            foreach(var interf in startingObject.GetInterfaces()) {
                FindAllTypes(types, interf);
            }
        }

        foreach(var method in startingObject.GetAllGraphQLMethods()) {
            if(method.HasCustomAttribute<GraphQLInternalAttribute>())
                continue;

            foreach(var parameter in method.GetAllGraphQLParameters()) {
                if(_ignore.Contains(parameter.ParameterType))
                    continue;

                _inputLevel++;
                _inputTypes.Add(Nullable.GetUnderlyingType(parameter.ParameterType) ?? parameter.ParameterType);
                FindAllTypes(types, parameter.ParameterType);
                _inputLevel--;
            }

            var graphQLType = method.ReturnType.GetGraphQLType();
            FindAllTypes(types, graphQLType);
        }

        if(!isOperation) {
            foreach(var property in startingObject.GetAllGraphQLProperties()) {
                if(_inputLevel > 0)
                    _inputTypes.Add(Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType);
                FindAllTypes(types, property.PropertyType);
            }
        }

        if(startingObject.IsInterface) {
            foreach(var implementingType in ReflectionCache.GetAllClassesThatImplements(startingObject)) {
                types.Add(_wrapperRegistry.GetWrapperOfSelf(implementingType));
            }
        }
    }

    private IEnumerable<__Field> CreateOperationFields(IEnumerable<OperationDescriptor> operations, Func<FederationSchema, string?> typeNameSelector) {
        var localOperations = operations.Select(x => x.Method).Where(x => !x.HasCustomAttribute<GraphQLInternalAttribute>() && !x.HasCustomAttribute<GraphQLFederatedAttribute>())
            .Select(query => new __Field(query) { Type = CreateType(query.ReturnType.DiscardTask(), true), Args = GetArguments(query).ToArray() });

        var federatedFields = new List<__Field>();
        foreach(var schema in _federationSchemas) {
            var operatorName = typeNameSelector(schema);
            if(operatorName == null)
                continue;

            var queryType = schema.Types.First(x => x.Name == operatorName);
            federatedFields.AddRange(queryType.Fields.Select(__Field.FromFederation));
        }

        return localOperations.Union(federatedFields);
    }

    private __Type CreateType(Type type, bool isTypeReference) {
        type = type.DiscardTask();

        var elementType = type.GetGraphQLBaseType();
        if(elementType.IsEnum) {
            if(type.IsCollection(out _)) {
                return new __Type(elementType) {
                    Kind = __TypeKind.LIST,
                    OfType = WrapNonNullable(Nullable.GetUnderlyingType(elementType) != null, CreateType(elementType, true))
                };
            }

            return new __Type(elementType, null, isTypeReference ? Array.Empty<__EnumValue>() : CreateEnumValues(elementType)) {
                Name = elementType.GetPrimitiveGraphQLName(),
                Kind = __TypeKind.ENUM,
            };
        }

        if(type.IsCollection()) {
            return new __Type(elementType) {
                Kind = __TypeKind.LIST,
                OfType = WrapNonNullable(Nullable.GetUnderlyingType(elementType) != null, CreateType(elementType, true))
            };
        }

        if(elementType == typeof(int)) {
            return new __Type(elementType) {
                Name = elementType.GetPrimitiveGraphQLName(),
                Kind = __TypeKind.SCALAR,
            };
        }

        if(elementType == typeof(string)) {
            return new __Type(elementType) {
                Name = elementType.GetPrimitiveGraphQLName(),
                Kind = __TypeKind.SCALAR
            };
        }

        if(elementType.IsSubclassOf(typeof(GraphQLId))) {
            return new __Type(elementType) {
                Name = elementType.GetPrimitiveGraphQLName(),
                Kind = __TypeKind.SCALAR
            };
        }

        if(elementType == typeof(bool)) {
            return new __Type(elementType) {
                Name = elementType.GetPrimitiveGraphQLName(),
                Kind = __TypeKind.SCALAR
            };
        }

        if(elementType == typeof(decimal)) {
            return new __Type(elementType) {
                Name = elementType.GetPrimitiveGraphQLName(),
                Kind = __TypeKind.SCALAR,
            };
        }

        if(_scalarRegistry.Contains(elementType)) {
            return new __Type(elementType) {
                Name = elementType.GetPrimitiveGraphQLName(),
                Kind = __TypeKind.SCALAR
            };
        }

        if(_inputTypes.Contains(elementType)) {
            var inputValues = new List<__InputValue>();

            foreach(var property in elementType.GetAllGraphQLProperties()) {
                __Type propertyType;
                if(NullabilityChecker.IsNullable(property)) {
                    var underlyingType = Nullable.GetUnderlyingType(property.PropertyType);
                    propertyType = CreateType(underlyingType ?? property.PropertyType, true);
                } else {
                    propertyType = WrapNonNullable(false, CreateType(property.PropertyType, true));
                }

                inputValues.Add(new __InputValue(property.GraphQLName(), property.GetDescription(), propertyType, null));
            }

            return new __Type(elementType, inputValues: inputValues) {
                Name = elementType.GetPrimitiveGraphQLName(),
                Kind = __TypeKind.INPUT_OBJECT,
                Description = elementType.GetDescription()
            };
        }

        var fields = new List<__Field>();
        if(!isTypeReference) {
            fields.AddRange(GetFields(elementType));
        }

        if(elementType.IsInterface) {
            var interfaceType = new __Type(elementType, fields) {
                Name = elementType.GetPrimitiveGraphQLName(),
                Kind = elementType.HasCustomAttribute<GraphQLUnionAttribute>() ? __TypeKind.UNION : __TypeKind.INTERFACE,
                Description = elementType.GetDescription()
            };

            if(!isTypeReference)
                interfaceType.PossibleTypes = ReflectionCache.GetAllClassesThatImplements(elementType).Select(x => CreateType(_wrapperRegistry.GetWrapperOfSelf(x), true)).ToArray();

            return interfaceType;
        }

        var objectType = new __Type(elementType, fields) {
            Name = elementType.GetPrimitiveGraphQLName(),
            Kind = __TypeKind.OBJECT,
            Description = elementType.GetDescription()
        };

        if(!isTypeReference)
            objectType.Interfaces = elementType.GetInterfaces().Select(x => CreateType(x, true)).Where(x => x.Kind == __TypeKind.INTERFACE).ToArray();

        return objectType;
    }

    private List<__Field> GetFields(Type baseType) {
        var fields = new List<__Field>();

        foreach(var method in baseType.GetAllGraphQLMethods()) {
            var returnType = method.ReturnType.DiscardTask();
            var isNullable = false;
            var underlyingType = Nullable.GetUnderlyingType(returnType);
            if(underlyingType != null) {
                returnType = underlyingType;
                isNullable = true;
            } else if(NullabilityChecker.IsNullable(method)) {
                isNullable = true;
            }

            fields.Add(new __Field(method) {
                Type = WrapNonNullable(isNullable, CreateType(returnType, true)),
                Args = GetArguments(method).ToArray(),
            });
        }

        foreach(var property in baseType.GetAllGraphQLProperties()) {
            var propertyType = property.PropertyType;
            var isNullable = NullabilityChecker.IsNullable(property);
            var underlyingType = Nullable.GetUnderlyingType(propertyType);
            if(underlyingType != null)
                propertyType = underlyingType;

            fields.Add(new __Field(property) {
                Name = property.GraphQLName(),
                Type = WrapNonNullable(isNullable, CreateType(propertyType, true))
            });
        }

        return fields;
    }

    private List<__InputValue> GetArguments(MethodInfo method) {
        var args = new List<__InputValue>();
        foreach(var parameter in method.GetAllGraphQLParameters()) {
            var propertyType = WrapNonNullable(NullabilityChecker.IsNullable(parameter), CreateType(parameter.ParameterType, true));
            args.Add(new __InputValue(parameter.GraphQLName(), parameter.GetDescription(), propertyType, GetDefaultValue(parameter)));
        }

        return args;
    }

    private static string? GetDefaultValue(ParameterInfo parameter) {
        var defaultValue = parameter.RawDefaultValue;
        if(defaultValue == null || defaultValue == DBNull.Value)
            return null;

        if(parameter.ParameterType == typeof(bool)) {
            return defaultValue.ToString()?.ToLower();
        }

        return defaultValue.ToString();
    }

    private __Type WrapNonNullable(bool nullable, __Type typeToBeWrapped) {
        if(nullable) {
            return typeToBeWrapped;
        }

        return new __Type(null) {
            Kind = __TypeKind.NON_NULL,
            OfType = typeToBeWrapped
        };
    }

    private __EnumValue[] CreateEnumValues(Type type) {
        return Enum.GetNames(type).Select(x => {
            var fieldInfo = type.GetField(x)!;
            var deprecationReason = fieldInfo.GetDeprecatedReason();
            var description = fieldInfo.GetDescription();
            return new __EnumValue(x, description, deprecationReason);
        }).ToArray();
    }
}