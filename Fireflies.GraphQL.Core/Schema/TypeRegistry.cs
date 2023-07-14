using Fireflies.GraphQL.Abstractions;
using Fireflies.GraphQL.Core.Extensions;
using Fireflies.GraphQL.Core.Federation;
using Fireflies.GraphQL.Core.Scalar;
using Fireflies.Utility.Reflection;

namespace Fireflies.GraphQL.Core.Schema;

internal class TypeRegistry {
    private readonly WrapperRegistry _wrapperRegistry;
    private readonly ScalarRegistry _scalarRegistry;
    private readonly GraphQLOptions _options;

    private readonly HashSet<Type> _inputTypes = new();
    private readonly HashSet<Type> _ignore = new();
    private readonly HashSet<Type> _allTypes = new();
    private int _inputLevel;

    public TypeRegistry(WrapperRegistry wrapperRegistry, ScalarRegistry scalarRegistry, GraphQLOptions options) {
        _wrapperRegistry = wrapperRegistry;
        _scalarRegistry = scalarRegistry;
        _options = options;

        _ignore.Add(typeof(__Schema));
    }

    public HashSet<Type> AllTypes => _allTypes;

    public void Initialize() {
        foreach(var type in _options.AllOperations.Where(x => !x.Method.HasCustomAttribute<GraphQLFederatedAttribute>()))
            FindAllTypes(type.Type, true, false);
    }

    private void FindAllTypes(Type startingObject, bool isOperation, bool isInput) {
        if(_ignore.Contains(startingObject))
            return;

        startingObject = _wrapperRegistry.GetWrapperOfSelf(startingObject);
        startingObject = startingObject.GetGraphQLBaseType();

        if(startingObject.IsCollection(out var elementType)) {
            FindAllTypes(elementType, false, isInput);
            return;
        }

        startingObject = Nullable.GetUnderlyingType(startingObject) ?? startingObject;

        if(_scalarRegistry.GetHandler(elementType, out var scalarHandler))
            startingObject = scalarHandler!.BaseType;

        if(!isOperation) {
            var typeIsAlreadyAdded = !_allTypes.Add(startingObject);
            if(typeIsAlreadyAdded)
                return;
        }

        if(isInput)
            _inputTypes.Add(startingObject);

        if(_scalarRegistry.Contains(startingObject))
            return;

        if(Type.GetTypeCode(startingObject) != TypeCode.Object)
            return;

        // Framework types without handlers are not allowed to be used
        if(startingObject.IsFrameworkType())
            throw new ArgumentException($"Framework type {startingObject.Name} does not have a scalar handler and is not allowed to be used. Register a scalar handler if use was intended");

        if(startingObject.IsInterface) {
            foreach(var impl in ReflectionCache.GetAllClassesThatImplements(startingObject))
                FindAllTypes(_wrapperRegistry.GetWrapperOfSelf(impl), false, isInput);
        } else {
            foreach(var interf in startingObject.GetInterfaces()) {
                FindAllTypes(interf, false, isInput);
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
                FindAllTypes(parameter.ParameterType, false, true);
                _inputLevel--;
            }

            var graphQLType = method.ReturnType.GetGraphQLType();
            FindAllTypes(graphQLType, false, isInput);
        }

        if(!isOperation) {
            foreach(var property in startingObject.GetAllGraphQLProperties()) {
                if(_inputLevel > 0)
                    _inputTypes.Add(Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType);
                FindAllTypes(property.PropertyType, false, isInput);
            }
        }

        if(startingObject.IsInterface) {
            foreach(var implementingType in ReflectionCache.GetAllClassesThatImplements(startingObject)) {
                _allTypes.Add(_wrapperRegistry.GetWrapperOfSelf(implementingType));
            }
        }
    }

    public bool ShouldIgnore(Type type) {
        return _ignore.Contains(type);
    }

    public bool IsInputType(Type type) {
        return _inputTypes.Contains(type);
    }
}