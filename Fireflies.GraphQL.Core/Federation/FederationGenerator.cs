using System.Reflection;
using System.Reflection.Emit;
using System.Text.Json.Nodes;
using Fireflies.GraphQL.Abstractions;
using Fireflies.GraphQL.Abstractions.Generator;
using Fireflies.GraphQL.Core.Extensions;
using Fireflies.GraphQL.Core.Federation.Schema;
using GraphQLParser.AST;

namespace Fireflies.GraphQL.Core.Federation;

public class FederationGenerator {
    private readonly (string Name, string Url) _federation;
    private readonly FederationSchema _federationSchema;
    private readonly ModuleBuilder _dynamicModule;

    public FederationGenerator((string Name, string Url) federation, FederationSchema federationSchema) {
        _federation = federation;
        _federationSchema = federationSchema;

        var assemblyName = new AssemblyName($"Fireflies.GraphQL.Federation.{_federation.Name}.ProxyAssembly");
        var dynamicAssembly = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);

        _dynamicModule = dynamicAssembly.DefineDynamicModule("Main");
    }

    public Type Generate() {
        var operationsTypeBuilder = _dynamicModule.DefineType($"{_federation.Name}Operations", TypeAttributes.Class | TypeAttributes.Public, typeof(FederationBase));
        operationsTypeBuilder.SetCustomAttribute(new CustomAttributeBuilder(typeof(GraphQLNoWrapperAttribute).GetConstructors().First(), Array.Empty<object>()));
        var baseConstructor = typeof(FederationBase).GetConstructor(BindingFlags.Public | BindingFlags.Instance, new[] { typeof(IGraphQLContext) })!;
        var baseParameters = baseConstructor.GetParameters();
        var constructorBuilder = operationsTypeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new[] { typeof(IGraphQLContext) });
        constructorBuilder.DefineParameter(1, ParameterAttributes.None, baseParameters[0].Name);

        var constructorIlGenerator = constructorBuilder.GetILGenerator();
        constructorIlGenerator.Emit(OpCodes.Ldarg_0);
        constructorIlGenerator.Emit(OpCodes.Ldarg_1);
        constructorIlGenerator.Emit(OpCodes.Call, baseConstructor);
        constructorIlGenerator.Emit(OpCodes.Ret);

        GenerateOperation(OperationType.Query, operationsTypeBuilder);
        GenerateOperation(OperationType.Mutation, operationsTypeBuilder);
        GenerateOperation(OperationType.Subscription, operationsTypeBuilder);

        return operationsTypeBuilder.CreateType()!;
    }

    private void GenerateOperation(OperationType operation, TypeBuilder operationsType) {
        var operationType = operation switch {
            OperationType.Query => _federationSchema.Types.FirstOrDefault(t => t.Name == _federationSchema.QueryType?.Name),
            OperationType.Mutation => _federationSchema.Types.FirstOrDefault(t => t.Name == _federationSchema.MutationType?.Name),
            OperationType.Subscription => _federationSchema.Types.FirstOrDefault(t => t.Name == _federationSchema.SubscriptionType?.Name),
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null)
        };

        if(operationType == null)
            return;

        foreach(var field in operationType.Fields) {
            if(field.Name.StartsWith("__"))
                continue;

            GenerateOperation(operation, operationsType, field);
        }
    }

    private void GenerateOperation(OperationType operation, TypeBuilder typeBuilder, FederationField field) {
        var taskReturnType = operation == OperationType.Subscription ? typeof(IAsyncEnumerable<JsonNode>) : typeof(Task<>).MakeGenericType(typeof(JsonNode));

        var methodBuilder = typeBuilder.DefineMethod(field.Name, MethodAttributes.Public, taskReturnType, new[] { typeof(ASTNode), typeof(ValueAccessor) });
        methodBuilder.SetCustomAttribute(new CustomAttributeBuilder(typeof(GraphQLFederatedAttribute).GetConstructors().First(), Array.Empty<object>()));

        methodBuilder.DefineAnonymousResolvedParameter(1);
        methodBuilder.DefineAnonymousResolvedParameter(2);

        AddOperationAttribute(operation, methodBuilder);

        var methodILGenerator = methodBuilder.GetILGenerator();

        var contextField = typeof(FederationBase).GetField("GraphQLContext", BindingFlags.NonPublic | BindingFlags.Instance)!;

        methodILGenerator.Emit(OpCodes.Ldarg_S, 1);
        methodILGenerator.Emit(OpCodes.Ldarg_S, 2);
        methodILGenerator.Emit(OpCodes.Ldarg_0);
        methodILGenerator.Emit(OpCodes.Ldfld, contextField);
        methodILGenerator.Emit(OpCodes.Ldstr, _federation.Url);
        methodILGenerator.Emit(OpCodes.Ldc_I4, (int)operation);
        if(operation != OperationType.Subscription) {
            var requestMethod = typeof(FederationHelper).GetMethod(nameof(FederationHelper.ExecuteRequest), BindingFlags.Static | BindingFlags.Public)!;
            methodILGenerator.EmitCall(OpCodes.Call, requestMethod, Type.EmptyTypes);
        } else {
            var subscribeMethod = typeof(FederationHelper).GetMethod(nameof(FederationHelper.ExecuteSubscription), BindingFlags.Static | BindingFlags.Public)!;
            methodILGenerator.Emit(OpCodes.Ldstr, field.Name);
            methodILGenerator.EmitCall(OpCodes.Call, subscribeMethod, Type.EmptyTypes);
        }

        methodILGenerator.Emit(OpCodes.Ret);
    }

    private static void AddOperationAttribute(OperationType operation, MethodBuilder methodBuilder) {
        switch(operation) {
            case OperationType.Query:
                methodBuilder.SetCustomAttribute(new CustomAttributeBuilder(typeof(GraphQLQueryAttribute).GetConstructors().First(), Array.Empty<object>()));
                break;
            case OperationType.Mutation:
                methodBuilder.SetCustomAttribute(new CustomAttributeBuilder(typeof(GraphQLMutationAttribute).GetConstructors().First(), Array.Empty<object>()));
                break;
            case OperationType.Subscription:
                methodBuilder.SetCustomAttribute(new CustomAttributeBuilder(typeof(GraphQLSubscriptionAttribute).GetConstructors().First(), Array.Empty<object>()));
                break;
        }
    }
}