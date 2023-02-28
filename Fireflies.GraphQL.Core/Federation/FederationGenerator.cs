using System.Reflection;
using System.Reflection.Emit;
using Fireflies.GraphQL.Abstractions;
using Fireflies.GraphQL.Core.Schema;
using GraphQLParser.AST;
using Newtonsoft.Json.Linq;

namespace Fireflies.GraphQL.Core.Federation;

public class FederationGenerator {
    private readonly (string Name, string Url) _federation;
    private readonly __Schema _federationSchema;
    private readonly ModuleBuilder _dynamicModule;

    private readonly Dictionary<string, Type> _nameLookup = new();

    public FederationGenerator((string Name, string Url) federation, __Schema federationSchema) {
        _federation = federation;
        _federationSchema = federationSchema;

        var assemblyName = new AssemblyName($"Fireflies.GraphQL.Federation.{_federation.Name}.ProxyAssembly");
        var dynamicAssembly = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);

        _dynamicModule = dynamicAssembly.DefineDynamicModule("Main");
    }

    public Type Generate() {
        var typesToGenerate = FindTypesToGenerate();
        foreach(var type in typesToGenerate.OrderBy(x => x.Kind is __TypeKind.INTERFACE or __TypeKind.UNION ? 0 : 1)) {
            GenerateType(type, _ => { });
        }

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

        foreach(var field in operationType.Fields(true)) {
            if(field.Name.StartsWith("__"))
                continue;

            GenerateOperation(operation, operationsType, field);
        }
    }

    private void GenerateOperation(OperationType operation, TypeBuilder typeBuilder, __Field field) {
        var argTypes = field.Args.Select(argType => GetTypeFromSchemaType(argType.Type)).ToList();

        var returnType = GetTypeFromSchemaType(field.Type);
        var taskReturnType = operation == OperationType.Subscription ? typeof(IAsyncEnumerable<>).MakeGenericType(returnType) : typeof(Task<>).MakeGenericType(returnType);

        var methodBuilder = typeBuilder.DefineMethod(field.Name, MethodAttributes.Public, taskReturnType, argTypes.Union(new[] { typeof(ASTNode) }).ToArray());
        DefineParameters(argTypes, methodBuilder, field);
        methodBuilder.DefineParameter(argTypes.Count + 1, ParameterAttributes.HasDefault | ParameterAttributes.Optional, Guid.NewGuid().ToString("N"))
            .SetCustomAttribute(new CustomAttributeBuilder(typeof(ResolvedAttribute).GetConstructors().First(), Array.Empty<object>()));

        AddAttributes(field, methodBuilder);
        AddOperationAttribute(operation, methodBuilder);

        var methodILGenerator = methodBuilder.GetILGenerator();

        var contextField = typeof(FederationBase).GetField("GraphQLContext", BindingFlags.NonPublic | BindingFlags.Instance)!;

        methodILGenerator.Emit(OpCodes.Ldarg_S, argTypes.Count + 1);
        methodILGenerator.Emit(OpCodes.Ldarg_0);
        methodILGenerator.Emit(OpCodes.Ldfld, contextField);
        methodILGenerator.Emit(OpCodes.Ldstr, _federation.Url);
        methodILGenerator.Emit(OpCodes.Ldc_I4, (int)operation);
        if(operation != OperationType.Subscription) {
            var requestMethod = typeof(FederationHelper).GetMethod(nameof(FederationHelper.ExecuteRequest), BindingFlags.Static | BindingFlags.Public)!.MakeGenericMethod(returnType);
            methodILGenerator.EmitCall(OpCodes.Call, requestMethod, Type.EmptyTypes);
        } else {
            var subscribeMethod = typeof(FederationHelper).GetMethod(nameof(FederationHelper.ExecuteSubscription), BindingFlags.Static | BindingFlags.Public)!.MakeGenericMethod(returnType);
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

    private Type GenerateType(__Type schemaType, Action<TypeBuilder>? extras = null, Type? interfaceType = null) {
        var typeName = GenerateName(schemaType);

        if(_nameLookup.TryGetValue(typeName, out var existingType)) {
            return existingType;
        }

        var isInterface = schemaType.Kind is __TypeKind.INTERFACE or __TypeKind.UNION;
        var isUnion = schemaType.Kind is __TypeKind.UNION;

        var baseType = isInterface ? null : typeof(FederationEntity);
        var generatedType = _dynamicModule.DefineType(typeName, isInterface ? TypeAttributes.Interface | TypeAttributes.Abstract : TypeAttributes.Class, baseType);
        generatedType.SetCustomAttribute(new CustomAttributeBuilder(typeof(GraphQLNoWrapperAttribute).GetConstructors().First(), Array.Empty<object>()));

        if(interfaceType != null) {
            generatedType.AddInterfaceImplementation(interfaceType);
        }

        if(!isInterface) {
            var baseConstructor = baseType!.GetConstructors(BindingFlags.Public | BindingFlags.Instance).First();
            var constructorBuilder = generatedType.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new[] { typeof(JObject) });
            var constructorGenerator = constructorBuilder.GetILGenerator();
            constructorGenerator.Emit(OpCodes.Ldarg_0);
            constructorGenerator.Emit(OpCodes.Ldarg_1);
            constructorGenerator.Emit(OpCodes.Call, baseConstructor);
            constructorGenerator.Emit(OpCodes.Ret);
        } else if(isUnion) {
            generatedType.SetCustomAttribute(new CustomAttributeBuilder(typeof(GraphQLUnionAttribute).GetConstructor(BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes)!, Array.Empty<object>()));
        }

        foreach(var field in schemaType.Fields(true)) {
            var argTypes = new List<Type>();
            foreach(var argType in field.Args) {
                var argumentType = GetTypeFromSchemaType(argType.Type);
                argTypes.Add(argumentType);
            }

            var returnType = GetTypeFromSchemaType(field.Type);
            var methodAttributes = isInterface ? MethodAttributes.Virtual | MethodAttributes.Abstract | MethodAttributes.Public : MethodAttributes.Public | MethodAttributes.Virtual;
            var fieldMethod = generatedType.DefineMethod(field.Name, methodAttributes, returnType, argTypes.ToArray());
            if(interfaceType != null) {
                var overridingMethod = interfaceType.GetMethod(field.Name, BindingFlags.Public | BindingFlags.Instance);
                if(overridingMethod != null) {
                    generatedType.DefineMethodOverride(fieldMethod, overridingMethod);
                }
            }

            AddAttributes(field, fieldMethod);
            DefineParameters(argTypes, fieldMethod, field);

            if(!isInterface) {
                var valueMethod = typeof(FederationHelper).GetMethod(nameof(FederationHelper.GetField), BindingFlags.Static | BindingFlags.Public)!.MakeGenericMethod(returnType);
                var instanceField = typeof(FederationEntity).GetField("GraphQLData", BindingFlags.NonPublic | BindingFlags.Instance)!;

                var fieldMethodILGenerator = fieldMethod.GetILGenerator();
                fieldMethodILGenerator.Emit(OpCodes.Ldarg_0);
                fieldMethodILGenerator.Emit(OpCodes.Ldfld, instanceField);
                fieldMethodILGenerator.Emit(OpCodes.Ldstr, field.Name);
                fieldMethodILGenerator.EmitCall(OpCodes.Call, valueMethod, Type.EmptyTypes);
                fieldMethodILGenerator.Emit(OpCodes.Ret);
            }
        }

        extras?.Invoke(generatedType);

        var finalType = generatedType.CreateType()!;
        _nameLookup.Add(typeName, finalType);

        if(isInterface) {
            foreach(var implementation in schemaType.PossibleTypes) {
                var implementationType = _federationSchema.Types.First(x => x.Name == implementation.Name);
                GenerateType(implementationType, null, finalType);
            }
        }

        return finalType;
    }

    private static void AddAttributes(__Field field, MethodBuilder fieldMethod) {
        if(field.Description != null) {
            fieldMethod.SetCustomAttribute(new CustomAttributeBuilder(typeof(GraphQLDescriptionAttribute).GetConstructor(BindingFlags.Public | BindingFlags.Instance, new[] { typeof(string) })!, new object?[] { field.Description }));
        }

        if(field.IsDeprecated) {
            fieldMethod.SetCustomAttribute(new CustomAttributeBuilder(typeof(GraphQLDeprecatedAttribute).GetConstructor(BindingFlags.Public | BindingFlags.Instance, new[] { typeof(string) })!, new object?[] { field.DeprecationReason }));
        }
    }

    private void DefineParameters(List<Type> argTypes, MethodBuilder fieldMethod, __Field field) {
        for(var i = 0; i < argTypes.Count; i++) {
            var parameterBuilder = fieldMethod.DefineParameter(i + 1, ParameterAttributes.None, field.Args[i].Name);
            if(field.Args[i].DefaultValue != null)
                parameterBuilder.SetConstant(Convert.ChangeType(field.Args[0].DefaultValue, argTypes[i]));
            else if(IsNullable(field.Args[i].Type))
                parameterBuilder.SetConstant(null);
        }
    }

    public bool IsNullable(__Type type) {
        if(type.Kind == __TypeKind.NON_NULL)
            return false;

        return true;
    }

    public IEnumerable<__Type> FindTypesToGenerate() {
        foreach(var type in _federationSchema.Types) {
            if(!ShouldGenerate(type))
                continue;

            yield return type;
        }
    }

    public Type GetTypeFromSchemaType(__Type type) {
        if(type.Name == "Int" || type.OfType?.Name == "Int")
            return typeof(int);

        if(type.Name == "Float" || type.OfType?.Name == "Float")
            return typeof(decimal);

        if(type.Name == "String" || type.OfType?.Name == "String")
            return typeof(string);

        if(type.Name == "Boolean" || type.OfType?.Name == "Boolean")
            return typeof(bool);

        if(type.Name == "ID" || type.OfType?.Name == "ID")
            throw new NotImplementedException("ID type is yet to be implemented");

        if(type.Kind == __TypeKind.NON_NULL)
            return GetTypeFromSchemaType(type.OfType!);

        if(type.Kind == __TypeKind.LIST)
            return typeof(IEnumerable<>).MakeGenericType(GetTypeFromSchemaType(type.OfType!));

        var generateName = GenerateName(type);
        if(_nameLookup.TryGetValue(generateName, out var existingType)) {
            return existingType;
        }

        return GenerateType(type, _ => { });
    }

    private string GenerateName(__Type type) {
        if(type.Kind == __TypeKind.LIST)
            return GenerateName(type.OfType!);

        if(type.Kind == __TypeKind.NON_NULL)
            return GenerateName(type.OfType!);

        return type.Name!;
    }

    public bool ShouldGenerate(__Type type) {
        if(type.Name!.StartsWith("__"))
            return false;

        if(type.Name == _federationSchema.QueryType?.Name || type.Name == _federationSchema.MutationType?.Name || type.Name == _federationSchema.SubscriptionType?.Name)
            return false;

        return type.Name is not ("Int" or "Float" or "String" or "Boolean" or "ID");
    }
}