﻿using System.Reflection;
using System.Reflection.Emit;
using System.Text.Json.Nodes;
using Fireflies.GraphQL.Abstractions;
using Fireflies.GraphQL.Abstractions.Generator;
using Fireflies.GraphQL.Abstractions.Schema;
using Fireflies.GraphQL.Core.Extensions;
using Fireflies.GraphQL.Core.Federation.Schema;
using Fireflies.GraphQL.Core.Generators.Connection;
using Fireflies.GraphQL.Core.Scalar;
using Fireflies.GraphQL.Core.Schema;
using GraphQLParser.AST;

namespace Fireflies.GraphQL.Core.Federation;

public class FederationGenerator {
    private readonly (string Name, string Url) _federation;
    private readonly FederationSchema _federationSchema;
    private readonly ScalarRegistry _scalarRegistry;
    private readonly ModuleBuilder _dynamicModule;

    private readonly Dictionary<string, Type> _nameLookup = new();

    public FederationGenerator((string Name, string Url) federation, FederationSchema federationSchema, ScalarRegistry scalarRegistry) {
        _federation = federation;
        _federationSchema = federationSchema;
        _scalarRegistry = scalarRegistry;

        var assemblyName = new AssemblyName($"Fireflies.GraphQL.Federation.{_federation.Name}.ProxyAssembly");
        var dynamicAssembly = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);

        _dynamicModule = dynamicAssembly.DefineDynamicModule("Main");
    }

    public Type Generate() {
        var typesToGenerate = FindTypesToGenerate();
        foreach(var type in typesToGenerate.OrderBy(x => x.Kind is __TypeKind.INTERFACE or __TypeKind.UNION ? 0 : 1)) {
            GenerateType(type);
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

        foreach(var field in operationType.Fields) {
            if(field.Name.StartsWith("__"))
                continue;

            GenerateOperation(operation, operationsType, field);
        }
    }

    private void GenerateOperation(OperationType operation, TypeBuilder typeBuilder, FederationField field) {
        var argTypes = field.Args.Select(argType => GetTypeFromSchemaType(argType.Type!)).ToList();

        var (returnType, _) = GetTypeFromSchemaType(field.Type);
        var taskReturnType = operation == OperationType.Subscription ? typeof(IAsyncEnumerable<>).MakeGenericType(returnType) : typeof(Task<>).MakeGenericType(returnType);

        var methodBuilder = typeBuilder.DefineMethod(field.Name, MethodAttributes.Public, taskReturnType, argTypes.Select(x => x.Type).Union(new[] { typeof(ASTNode), typeof(ValueAccessor) }).ToArray());
        DefineParameters(argTypes, methodBuilder, field);
        methodBuilder.DefineAnonymousResolvedParameter(argTypes.Count + 1);
        methodBuilder.DefineAnonymousResolvedParameter(argTypes.Count + 2);

        AddAttributes(field, methodBuilder);
        AddOperationAttribute(operation, methodBuilder);

        var methodILGenerator = methodBuilder.GetILGenerator();

        var contextField = typeof(FederationBase).GetField("GraphQLContext", BindingFlags.NonPublic | BindingFlags.Instance)!;

        methodILGenerator.Emit(OpCodes.Ldarg_S, argTypes.Count + 1);
        methodILGenerator.Emit(OpCodes.Ldarg_S, argTypes.Count + 2);
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

    private Type GenerateType(FederationType schemaType, Type? interfaceType = null) {
        var typeName = GenerateName(schemaType);

        if(_nameLookup.TryGetValue(typeName, out var existingType))
            return existingType;

        if(_scalarRegistry.NameToType(typeName, out var existingScalarType))
            return existingScalarType!;

        if(schemaType.Kind == __TypeKind.ENUM)
            return GenerateEnum(schemaType, typeName);

        return GenerateObject(schemaType, interfaceType, typeName);
    }

    private Type GenerateEnum(FederationType schemaType, string typeName) {
        var generatedType = _dynamicModule.DefineEnum(schemaType.Name!, TypeAttributes.Public, typeof(int));
        _nameLookup.Add(typeName, generatedType);

        var id = 0;
        foreach(var enumValue in schemaType.EnumValues) {
            var literalBuilder = generatedType.DefineLiteral(enumValue.Name, id++);
            if(enumValue.Description != null)
                literalBuilder.SetCustomAttribute(new CustomAttributeBuilder(typeof(GraphQLDescriptionAttribute).GetConstructor(BindingFlags.Public | BindingFlags.Instance, new[] { typeof(string) })!, new object?[] { enumValue.Description }));
            if(enumValue.IsDeprecated)
                literalBuilder.SetCustomAttribute(new CustomAttributeBuilder(typeof(GraphQLDeprecatedAttribute).GetConstructor(BindingFlags.Public | BindingFlags.Instance, new[] { typeof(string) })!, new object?[] { enumValue.DeprecationReason }));
        }

        return generatedType.CreateType()!;
    }

    private Type GenerateObject(FederationType schemaType, Type? interfaceType, string typeName) {
        var isInterface = schemaType.Kind is __TypeKind.INTERFACE or __TypeKind.UNION;
        var isUnion = schemaType.Kind is __TypeKind.UNION;

        var baseType = GetBaseType(schemaType);
        var generatedType = _dynamicModule.DefineType(typeName, isInterface ? TypeAttributes.Interface | TypeAttributes.Abstract : TypeAttributes.Class, baseType);
        _nameLookup.Add(typeName, generatedType);

        generatedType.SetCustomAttribute(new CustomAttributeBuilder(typeof(GraphQLNoWrapperAttribute).GetConstructors().First(), Array.Empty<object>()));

        if(interfaceType != null) {
            generatedType.AddInterfaceImplementation(interfaceType);
        }

        if(!isInterface) {
            if(schemaType.Kind == __TypeKind.INPUT_OBJECT) {
                generatedType.DefineDefaultConstructor(MethodAttributes.Public);
            } else {
                var baseConstructor = baseType!.GetConstructors(BindingFlags.Public | BindingFlags.Instance).First();
                var constructorBuilder = generatedType.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new[] { typeof(JsonObject) });
                var constructorGenerator = constructorBuilder.GetILGenerator();
                constructorGenerator.Emit(OpCodes.Ldarg_0);
                constructorGenerator.Emit(OpCodes.Ldarg_1);
                constructorGenerator.Emit(OpCodes.Call, baseConstructor);
                constructorGenerator.Emit(OpCodes.Ret);
            }
        } else if(isUnion) {
            generatedType.SetCustomAttribute(new CustomAttributeBuilder(typeof(GraphQLUnionAttribute).GetConstructor(BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes)!, Array.Empty<object>()));
        }

        foreach(var inputField in schemaType.InputFields) {
            var (returnType, isNullable) = GetTypeFromSchemaType(inputField.Type);

            var propertyBuilder = generatedType.DefineProperty(inputField.Name, PropertyAttributes.None, returnType, Type.EmptyTypes);
            var field = generatedType.DefineField($"_{inputField.Name.LowerCaseFirstLetter()}", returnType, FieldAttributes.Private);

            if(isNullable)
                propertyBuilder.SetCustomAttribute(new CustomAttributeBuilder(typeof(GraphQLNullable).GetConstructors().First(), Array.Empty<object>()));

            var getMethod = generatedType.DefineMethod($"get_{inputField.Name}", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.Virtual, CallingConventions.Standard, returnType, Type.EmptyTypes);
            propertyBuilder.SetGetMethod(getMethod);
            var getILGenerator = getMethod.GetILGenerator();
            getILGenerator.Emit(OpCodes.Ldarg_0);
            getILGenerator.Emit(OpCodes.Ldfld, field);
            getILGenerator.Emit(OpCodes.Ret);

            var setMethod = generatedType.DefineMethod($"set_{inputField.Name}", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.Virtual, CallingConventions.Standard, typeof(void), new[] { returnType });
            var setILGenerator = setMethod.GetILGenerator();
            setILGenerator.Emit(OpCodes.Ldarg_0);
            setILGenerator.Emit(OpCodes.Ldarg_1);
            setILGenerator.Emit(OpCodes.Stfld, field);
            setILGenerator.Emit(OpCodes.Ret);
            propertyBuilder.SetSetMethod(setMethod);
        }

        foreach(var field in schemaType.Fields) {
            var argTypes = field.Args.Select(argType => GetTypeFromSchemaType(argType.Type!)).ToList();

            var (returnType, _) = GetTypeFromSchemaType(field.Type);
            var methodAttributes = isInterface ? MethodAttributes.Virtual | MethodAttributes.Abstract | MethodAttributes.Public : MethodAttributes.Public | MethodAttributes.Virtual;
            var fieldMethod = generatedType.DefineMethod(field.Name, methodAttributes, returnType, argTypes.Select(x => x.Type).ToArray());
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

        var finalType = generatedType.CreateType()!;

        if(isInterface) {
            foreach(var implementation in schemaType.PossibleTypes) {
                var implementationType = _federationSchema.Types.First(x => x.Name == implementation.Name);
                GenerateType(implementationType, finalType);
            }
        }

        return finalType;
    }

    private static Type? GetBaseType(FederationType type) {
        var isInterface = type.Kind is __TypeKind.INTERFACE or __TypeKind.UNION;
        if(isInterface)
            return null;

        return type.Kind == __TypeKind.INPUT_OBJECT ? typeof(object) : typeof(FederationEntity);
    }

    private static void AddAttributes(FederationFieldBase field, MethodBuilder fieldMethod) {
        if(field.Description != null)
            fieldMethod.SetCustomAttribute(new CustomAttributeBuilder(typeof(GraphQLDescriptionAttribute).GetConstructor(BindingFlags.Public | BindingFlags.Instance, new[] { typeof(string) })!, new object?[] { field.Description }));
        if(field is FederationField { IsDeprecated: true } realField)
            fieldMethod.SetCustomAttribute(new CustomAttributeBuilder(typeof(GraphQLDeprecatedAttribute).GetConstructor(BindingFlags.Public | BindingFlags.Instance, new[] { typeof(string) })!, new object?[] { realField.DeprecationReason }));
    }

    private void DefineParameters(List<(Type Type, bool IsNullable)> argTypes, MethodBuilder fieldMethod, FederationField field) {
        for(var i = 0; i < argTypes.Count; i++) {
            var parameterBuilder = fieldMethod.DefineParameter(i + 1, ParameterAttributes.None, field.Args[i].Name);
            if(argTypes[i].IsNullable) {
                parameterBuilder.SetCustomAttribute(new CustomAttributeBuilder(typeof(GraphQLNullable).GetConstructors().First(), Array.Empty<object>()));
                parameterBuilder.SetConstant(null);
            }

            if(field.Args[i].DefaultValue != null)
                parameterBuilder.SetConstant(Convert.ChangeType(field.Args[i].DefaultValue, argTypes[i].Type));
        }
    }

    public bool IsNullable(FederationType type) {
        if(type.Kind == __TypeKind.NON_NULL)
            return false;

        return true;
    }

    public IEnumerable<FederationType> FindTypesToGenerate() {
        foreach(var type in _federationSchema.Types) {
            if(!ShouldGenerate(type))
                continue;

            yield return type;
        }
    }

    public (Type Type, bool IsNullable) GetTypeFromSchemaType(FederationType type) {
        if(type.Name == "Int" || type.OfType?.Name == "Int")
            return (typeof(int), true);

        if(type.Name == "Float" || type.OfType?.Name == "Float")
            return (typeof(decimal), true);

        if(type.Name == "String" || type.OfType?.Name == "String")
            return (typeof(string), true);

        if(type.Name == "Boolean" || type.OfType?.Name == "Boolean")
            return (typeof(bool), true);

        if(type.Name == "ID" || type.OfType?.Name == "ID")
            return (typeof(GraphQLId<string>), true);

        if(type.Kind == __TypeKind.SCALAR) {
            if(_scalarRegistry.NameToType(type.Name, out var existingScalarType))
                return (existingScalarType!, true);

            var scalarType = GenerateScalarType(type);
            _scalarRegistry.AddScalar(scalarType, new FederatedScalarHandler());
            return (scalarType, true);
        }

        if(type.Kind == __TypeKind.NON_NULL)
            return (GetTypeFromSchemaType(type.OfType!).Type, false);

        if(type.Kind == __TypeKind.LIST)
            return (typeof(IEnumerable<>).MakeGenericType(GetTypeFromSchemaType(type.OfType!).Type), true);

        if(type.Kind == __TypeKind.OBJECT && type.Name == nameof(PageInfo))
            return (typeof(PageInfo), false);

        var generateName = GenerateName(type);
        if(_nameLookup.TryGetValue(generateName, out var existingType)) {
            return (existingType, true);
        }

        var rootType = _federationSchema.Types.First(x => x.Name == type.Name);
        return (GenerateType(rootType), true);
    }

    private Type? GenerateScalarType(FederationType type) {
        var scalarTypeBuilder = _dynamicModule.DefineType(type.Name!, TypeAttributes.Public, typeof(GraphQLScalar));
        var constructorBuilder = scalarTypeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new[] { typeof(string) });
        var constructorIlGenerator = constructorBuilder.GetILGenerator();
        constructorIlGenerator.Emit(OpCodes.Ldarg_0);
        constructorIlGenerator.Emit(OpCodes.Ldarg_1);
        constructorIlGenerator.Emit(OpCodes.Call, typeof(GraphQLScalar).GetConstructor(BindingFlags.Public | BindingFlags.Instance, new[] { typeof(string) })!);
        constructorIlGenerator.Emit(OpCodes.Ret);

        var scalarType = scalarTypeBuilder.CreateType();
        return scalarType;
    }

    private string GenerateName(FederationType type) {
        if(type.Kind == __TypeKind.LIST)
            return GenerateName(type.OfType!);

        if(type.Kind == __TypeKind.NON_NULL)
            return GenerateName(type.OfType!);

        return type.Name!;
    }

    public bool ShouldGenerate(FederationType type) {
        if(type.Name!.StartsWith("__"))
            return false;

        if(type.Name == _federationSchema.QueryType?.Name || type.Name == _federationSchema.MutationType?.Name || type.Name == _federationSchema.SubscriptionType?.Name)
            return false;

        if(type.Name is ("Int" or "Float" or "String" or "Boolean" or "ID"))
            return false;

        if(_scalarRegistry.NameToType(type.Name, out _))
            return false;

        return true;
    }
}