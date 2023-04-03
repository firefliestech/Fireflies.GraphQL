using System.Reflection;
using Fireflies.GraphQL.Abstractions;
using Fireflies.GraphQL.Core.Exceptions;
using Fireflies.GraphQL.Core.Extensions;
using Fireflies.GraphQL.Core.Scalar;
using Fireflies.Utility.Reflection;

namespace Fireflies.GraphQL.Core;

internal class SchemaValidator {
    private readonly IEnumerable<OperationDescriptor> _operations;
    private readonly ScalarRegistry _scalarRegistry;
    private readonly Dictionary<string, Type> _verifiedTyped = new();

    public SchemaValidator(IEnumerable<OperationDescriptor> operations, ScalarRegistry scalarRegistry) {
        _operations = operations;
        _scalarRegistry = scalarRegistry;
    }

    public void Validate() {
        foreach(var operation in _operations) {
            var returnType = operation.Method.ReturnType.DiscardTask();
            InspectType(returnType);
        }
    }

    private void InspectType(Type type) {
        if(type.IsCollection(out var elementType)) {
            type = elementType;
        }

        if(!_scalarRegistry.IsValidGraphQLObjectType(type) || type.IsSubclassOf(typeof(GraphQLId)))
            return;

        if(type == typeof(void))
            return;

        var graphQLName = type.GraphQLName();
        if(_verifiedTyped.TryGetValue(graphQLName, out var existingType)) {
            if(existingType != type) {
                throw new DuplicateNameException($"{type.Name} is used for more than one type");
            }

            return;
        }

        _verifiedTyped.Add(graphQLName, type);

        foreach(var subType in type.GetAllGraphQLMemberInfo()) {
            Type typeToInspect;
            if(subType is PropertyInfo propertyInfo)
                typeToInspect = propertyInfo.PropertyType;
            else if(subType is MethodInfo methodInfo)
                typeToInspect = methodInfo.ReturnType;
            else
                throw new ArgumentOutOfRangeException(nameof(subType));

            typeToInspect = typeToInspect.DiscardTask();
            typeToInspect = Nullable.GetUnderlyingType(typeToInspect) ?? typeToInspect;

            InspectType(typeToInspect);
        }
    }
}