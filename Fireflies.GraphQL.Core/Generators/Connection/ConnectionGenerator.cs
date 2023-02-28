using System.Reflection;
using System.Reflection.Emit;
using Fireflies.GraphQL.Abstractions;
using Fireflies.GraphQL.Core.Exceptions;
using Fireflies.GraphQL.Core.Extensions;

namespace Fireflies.GraphQL.Core.Generators.Connection;

public class ConnectionGenerator : ITypeExtenderGenerator {
    private readonly ModuleBuilder _moduleBuilder;

    public ConnectionGenerator(ModuleBuilder moduleBuilder) {
        _moduleBuilder = moduleBuilder;
    }

    public void Extend(TypeBuilder typeBuilder, MethodBuilder wrappedMethod, MemberInfo baseMember, FieldBuilder instanceField, MethodExtenderDescriptor[] decoratorDescriptors) {
        if(!baseMember.HasCustomAttribute<GraphQlPaginationAttribute>())
            return;

        var baseReturnType = baseMember switch {
            MethodInfo methodInfo => methodInfo.ReturnType,
            PropertyInfo propertyInfo => propertyInfo.PropertyType,
            _ => throw new ArgumentOutOfRangeException(nameof(baseMember), baseMember, null)
        };

        var baseParameters = baseMember switch {
            MethodInfo methodInfo => methodInfo.GetParameters(),
            PropertyInfo => Array.Empty<ParameterInfo>(),
            _ => throw new ArgumentOutOfRangeException(nameof(baseMember), baseMember, null)
        };

        if(!baseReturnType.IsCollection(out var baseElementType))
            throw new GraphQLTypeException($"Cant add pagination for {baseElementType} because return type is not IEnumerable");

        if(!baseElementType.GetProperties(BindingFlags.Public | BindingFlags.Instance).Any(x => x.HasCustomAttribute<GraphQlIdAttribute>()))
            throw new GraphQLTypeException($"Cant add pagination for {baseElementType} because return type does not have any {nameof(GraphQlIdAttribute)} attributes");

        var returnType = wrappedMethod.ReturnType;
        returnType.IsCollection(out var elementType);

        var connectorTypeName = $"{wrappedMethod.Name}Connection";
        var (connectionType, edgeType) = GenerateConnectionType(connectorTypeName, elementType);
        var methodParameters = new List<Type>();
        var decoratedParameters = decoratorDescriptors.SelectMany(x => x.ParameterTypes).ToArray();
        methodParameters.AddRange(baseParameters.Select(x => x.ParameterType));
        methodParameters.AddRange(decoratedParameters);
        methodParameters.Add(typeof(int));
        methodParameters.Add(typeof(string));

        var connectionReturnType = typeof(Task<>).MakeGenericType(connectionType);
        var methodBuilder = typeBuilder.DefineMethod(connectorTypeName,
            MethodAttributes.Public,
            CallingConventions.Standard,
            connectionReturnType,
            methodParameters.ToArray());

        baseMember.CopyAttributes(ab => methodBuilder.SetCustomAttribute(ab));

        methodBuilder.DefineParameters(baseParameters);
        foreach(var parameter in decoratorDescriptors)
            parameter.DefineParametersCallback(methodBuilder);

        var baseParametersLength = baseParameters.Length + decoratedParameters.Length;
        methodBuilder.DefineParameter(baseParametersLength + 1, ParameterAttributes.HasDefault, "first").SetConstant(10);
        methodBuilder.DefineParameter(baseParametersLength + 2, ParameterAttributes.HasDefault, "after").SetConstant(null);

        var methodIlGenerator = methodBuilder.GetILGenerator();

        methodIlGenerator.Emit(OpCodes.Ldarg_0);
        for(var i = 1; i <= baseParametersLength; i++) {
            methodIlGenerator.Emit(OpCodes.Ldarg_S, i);
        }

        methodIlGenerator.EmitCall(OpCodes.Call, wrappedMethod, null);

        if(!baseReturnType.IsTask()) {
            var taskFromResultMethod = typeof(Task).GetMethod(nameof(Task.FromResult), BindingFlags.Public | BindingFlags.Static)!.MakeGenericMethod(connectionType);
            methodIlGenerator.EmitCall(OpCodes.Call, taskFromResultMethod, null);
        }

        methodIlGenerator.Emit(OpCodes.Ldarg_S, baseParametersLength + 1); // first
        methodIlGenerator.Emit(OpCodes.Ldarg_S, baseParametersLength + 2); // after

        var createConnectionMethod = typeof(WrapperHelper).GetMethod(nameof(WrapperHelper.CreateEnumerableTaskConnection), BindingFlags.Public | BindingFlags.Static)!.MakeGenericMethod(connectionType, edgeType, elementType);
        methodIlGenerator.EmitCall(OpCodes.Call, createConnectionMethod, null);

        methodIlGenerator.Emit(OpCodes.Ret);
    }

    private (Type, Type) GenerateConnectionType(string typeName, Type nodeType) {
        var edgeType = GenerateEdgeType(nodeType);
        var baseType = typeof(ConnectionBase<,>).MakeGenericType(edgeType, nodeType);
        var connectionType = _moduleBuilder.DefineType(typeName,
            TypeAttributes.Public |
            TypeAttributes.Class |
            TypeAttributes.AutoClass |
            TypeAttributes.AnsiClass |
            TypeAttributes.BeforeFieldInit |
            TypeAttributes.AutoLayout,
            baseType);

        var parameterTypes = new[] { typeof(IEnumerable<>).MakeGenericType(edgeType), typeof(int), typeof(string) };
        var baseConstructor = baseType.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance).First();

        // Build constructor
        var constructorBuilder = connectionType.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, parameterTypes);
        var constructorIlGenerator = constructorBuilder.GetILGenerator();

        // Call base constructor
        constructorIlGenerator.Emit(OpCodes.Ldarg_0);
        constructorIlGenerator.Emit(OpCodes.Ldarg_1);
        constructorIlGenerator.Emit(OpCodes.Ldarg_2);
        constructorIlGenerator.Emit(OpCodes.Ldarg_3);
        constructorIlGenerator.Emit(OpCodes.Call, baseConstructor);
        constructorIlGenerator.Emit(OpCodes.Ret);

        return (connectionType.CreateType()!, edgeType);
    }

    private Type GenerateEdgeType(Type nodeType) {
        var baseType = typeof(EdgeBase<>).MakeGenericType(nodeType);
        var edgeType = _moduleBuilder.DefineType($"{nodeType.Name}Edge",
            TypeAttributes.Public |
            TypeAttributes.Class |
            TypeAttributes.AutoClass |
            TypeAttributes.AnsiClass |
            TypeAttributes.BeforeFieldInit |
            TypeAttributes.AutoLayout,
            baseType);

        var baseConstructor = baseType.GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, new[] { nodeType })!;

        // Build constructor
        var constructorBuilder = edgeType.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new[] { nodeType });
        var constructorIlGenerator = constructorBuilder.GetILGenerator();

        // Call base constructor
        constructorIlGenerator.Emit(OpCodes.Ldarg_0);
        constructorIlGenerator.Emit(OpCodes.Ldarg_1);
        constructorIlGenerator.Emit(OpCodes.Call, baseConstructor);
        constructorIlGenerator.Emit(OpCodes.Ret);

        return edgeType.CreateType()!;
    }
}
