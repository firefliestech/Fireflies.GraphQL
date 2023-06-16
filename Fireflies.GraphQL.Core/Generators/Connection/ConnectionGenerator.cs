using System.Reflection;
using System.Reflection.Emit;
using Fireflies.GraphQL.Abstractions;
using Fireflies.GraphQL.Abstractions.Connection;
using Fireflies.GraphQL.Abstractions.Generator;
using Fireflies.GraphQL.Core.Exceptions;
using Fireflies.GraphQL.Core.Extensions;
using Fireflies.Utility.Reflection;

namespace Fireflies.GraphQL.Core.Generators.Connection;

public class ConnectionGenerator : ITypeExtenderGenerator {
    private readonly ModuleBuilder _moduleBuilder;

    public ConnectionGenerator(ModuleBuilder moduleBuilder) {
        _moduleBuilder = moduleBuilder;
    }

    public void Extend(TypeBuilder typeBuilder, MethodBuilder wrappedMethod, BaseDescriptor baseDescriptor) {
        if(!baseDescriptor.MemberInfo.HasCustomAttribute<GraphQLPaginationAttribute>())
            return;

        var baseReturnType = baseDescriptor.ReturnType;

        if(!baseReturnType.IsCollection(out var baseElementType))
            throw new GraphQLTypeException($"Cant add pagination for {baseElementType} because return type is not IEnumerable");

        if(!baseElementType.GetProperties(BindingFlags.Public | BindingFlags.Instance).Any(x => x.HasCustomAttribute<GraphQLIdAttribute>()))
            throw new GraphQLTypeException($"Cant add pagination for {baseElementType} because return type does not have any {nameof(GraphQLIdAttribute)} attributes");

        var returnType = wrappedMethod.ReturnType;
        returnType.IsCollection(out var elementType);

        var connectorTypeName = $"{wrappedMethod.Name}Connection";
        var (connectionType, edgeType) = GenerateConnectionType(connectorTypeName, elementType);
        var methodParameters = new List<Type>();
        methodParameters.AddRange(baseDescriptor.ParameterTypes);
        methodParameters.Add(typeof(int));
        methodParameters.Add(typeof(string));

        var connectionReturnType = typeof(Task<>).MakeGenericType(connectionType);
        var methodBuilder = typeBuilder.DefineMethod(connectorTypeName,
            MethodAttributes.Public,
            CallingConventions.Standard,
            connectionReturnType,
            methodParameters.ToArray());

        baseDescriptor.MemberInfo.CopyAttributes(ab => methodBuilder.SetCustomAttribute(ab), x => x != typeof(GraphQLInternalAttribute));

        foreach(var defineCallback in baseDescriptor.DefineParameterCallbacks)
            defineCallback(methodBuilder);

        var baseParametersLength = baseDescriptor.ParameterTypes.Count();
        methodBuilder.DefineParameter(baseParametersLength + 1, ParameterAttributes.HasDefault, "first").SetConstant(10);
        methodBuilder.DefineParameter(baseParametersLength + 2, ParameterAttributes.HasDefault | ParameterAttributes.Optional, "after").AsNullable();

        if(!baseDescriptor.GeneratingInterface) {
            var methodIlGenerator = methodBuilder.GetILGenerator();

            methodIlGenerator.Emit(OpCodes.Ldarg_0);
            for(var i = 1; i <= baseParametersLength; i++)
                methodIlGenerator.Emit(OpCodes.Ldarg_S, i);

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

        baseType.CopyAttributes(a => connectionType.SetCustomAttribute(a));
        
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

        baseType.CopyAttributes(a => edgeType.SetCustomAttribute(a));

        var baseConstructor = baseType.GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, new[] { nodeType })!;

        // Build constructor
        var constructorBuilder = edgeType.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new[] { nodeType });
        var constructorIlGenerator = constructorBuilder.GetILGenerator();

        // Call base constructor
        constructorIlGenerator.Emit(OpCodes.Ldarg_0);
        constructorIlGenerator.Emit(OpCodes.Ldarg_1);
        constructorIlGenerator.Emit(OpCodes.Call, baseConstructor);
        constructorIlGenerator.Emit(OpCodes.Ret);

        var createdType = edgeType.CreateType()!;
        return createdType;
    }
}
