using System.Reflection;
using System.Reflection.Emit;
using Fireflies.GraphQL.Contract;
using Fireflies.GraphQL.Core.Schema;
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
        foreach(var type in typesToGenerate) {
            GenerateType(type, tb => { });
        }

        var operationsTypeBuilder = _dynamicModule.DefineType("Operations", TypeAttributes.Class | TypeAttributes.Public, typeof(FederationBase));
        var baseConstructor = typeof(FederationBase).GetConstructor(BindingFlags.Public | BindingFlags.Instance, new[] { typeof(GraphQLContext) })!;
        var baseParameters = baseConstructor.GetParameters();
        var constructorBuilder = operationsTypeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new[] { typeof(GraphQLContext) });
        constructorBuilder.DefineParameter(1, ParameterAttributes.None, baseParameters[0].Name);

        var constructorIlGenerator = constructorBuilder.GetILGenerator();
        var i = 0;
        constructorIlGenerator.Emit(OpCodes.Ldarg_S, i++);
        constructorIlGenerator.Emit(OpCodes.Ldarg_S, i++);
        constructorIlGenerator.Emit(OpCodes.Ldstr, _federation.Url);
        constructorIlGenerator.Emit(OpCodes.Ldstr, _federation.Name);
        constructorIlGenerator.Emit(OpCodes.Call, baseConstructor);
        constructorIlGenerator.Emit(OpCodes.Ret);

        GenerateOperation(_federationSchema.QueryType?.Name, operationsTypeBuilder, mb => mb.SetCustomAttribute(new CustomAttributeBuilder(typeof(GraphQLQueryAttribute).GetConstructors().First(), Array.Empty<object>())));
        GenerateOperation(_federationSchema.MutationType?.Name, operationsTypeBuilder, mb => mb.SetCustomAttribute(new CustomAttributeBuilder(typeof(GraphQLMutationAttribute).GetConstructors().First(), Array.Empty<object>())));
        GenerateOperation(_federationSchema.SubscriptionType?.Name, operationsTypeBuilder, mb => mb.SetCustomAttribute(new CustomAttributeBuilder(typeof(GraphQLSubscriptionAttribute).GetConstructors().First(), Array.Empty<object>())));

        return operationsTypeBuilder.CreateType()!;
    }

    private void GenerateOperation(string? typeName, TypeBuilder operationsType, Action<MethodBuilder> addExtrasCallback) {
        var operationType = _federationSchema.Types.FirstOrDefault(t => t.Name == typeName);
        if(operationType == null) {
            return;
        }

        foreach(var field in operationType.Fields(true)) {
            if(field.Name.StartsWith("__"))
                continue;

            GenerateOperation(operationsType, field, addExtrasCallback);
        }
    }

    private void GenerateOperation(TypeBuilder typeBuilder, __Field field, Action<MethodBuilder> addExtrasCallback) {
        var argTypes = new List<Type>();
        foreach(var argType in field.Args) {
            var argumentType = GetTypeFromSchemaType(argType.Type);
            argTypes.Add(argumentType);
        }

        var returnType = GetTypeFromSchemaType(field.Type);
        var taskReturnType = typeof(Task<>).MakeGenericType(returnType);
        var methodBuilder = typeBuilder.DefineMethod(field.Name, MethodAttributes.Public, taskReturnType, argTypes.ToArray());
        DefineParameters(argTypes, methodBuilder, field);
        AddAttributes(field, methodBuilder);

        var fieldMethodILGenerator = methodBuilder.GetILGenerator();

        var astNodeProperty = typeof(FederationBase).GetProperty(nameof(FederationBase.ASTNode), BindingFlags.Public | BindingFlags.Instance)!;
        var contextField = typeof(FederationBase).GetField("_context", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var executeMethod = typeof(FederationHelper).GetMethod(nameof(FederationHelper.ExecuteRequest), BindingFlags.Static | BindingFlags.Public)!.MakeGenericMethod(returnType);

        //var executeRequest = typeof(FederationBase).GetMethod("ExecuteRequest", BindingFlags.NonPublic | BindingFlags.Instance)!.MakeGenericMethod(returnType);
        fieldMethodILGenerator.Emit(OpCodes.Ldarg_0);
        fieldMethodILGenerator.EmitCall(OpCodes.Call, astNodeProperty.GetMethod!, Type.EmptyTypes);
        fieldMethodILGenerator.Emit(OpCodes.Ldfld, contextField);
        fieldMethodILGenerator.Emit(OpCodes.Ldstr, _federation.Url);
        fieldMethodILGenerator.EmitCall(OpCodes.Call, executeMethod, Type.EmptyTypes);
        fieldMethodILGenerator.Emit(OpCodes.Ret);

        addExtrasCallback(methodBuilder);
    }

    private Type GenerateType(__Type schemaType, Action<TypeBuilder> extras) {
        var typeName = GenerateName(schemaType);

        var baseType = typeof(FederationEntity);
        var generatedType = _dynamicModule.DefineType(typeName, TypeAttributes.Class, baseType);
        var baseConstructor = baseType.GetConstructors(BindingFlags.Public | BindingFlags.Instance).First();
        var constructorBuilder = generatedType.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new[] { typeof(JObject) });
        var constructorGenerator = constructorBuilder.GetILGenerator();
        constructorGenerator.Emit(OpCodes.Ldarg_0);
        constructorGenerator.Emit(OpCodes.Ldarg_1);
        constructorGenerator.Emit(OpCodes.Call, baseConstructor);
        constructorGenerator.Emit(OpCodes.Ret);

        foreach(var field in schemaType.Fields(true)) {
            var argTypes = new List<Type>();
            foreach(var argType in field.Args) {
                var argumentType = GetTypeFromSchemaType(argType.Type);
                argTypes.Add(argumentType);
            }

            var returnType = GetTypeFromSchemaType(field.Type);
            var fieldMethod = generatedType.DefineMethod(field.Name, MethodAttributes.Public, returnType, argTypes.ToArray());
            AddAttributes(field, fieldMethod);
            DefineParameters(argTypes, fieldMethod, field);

            var valueMethod = typeof(FederationHelper).GetMethod(nameof(FederationHelper.GetField), BindingFlags.Static | BindingFlags.Public)!.MakeGenericMethod(returnType);
            var instanceField = baseType.GetField("_data", BindingFlags.NonPublic | BindingFlags.Instance);

            var fieldMethodILGenerator = fieldMethod.GetILGenerator();
            fieldMethodILGenerator.Emit(OpCodes.Ldarg_0);
            fieldMethodILGenerator.Emit(OpCodes.Ldfld, instanceField);
            fieldMethodILGenerator.Emit(OpCodes.Ldstr, field.Name);
            fieldMethodILGenerator.EmitCall(OpCodes.Call, valueMethod, Type.EmptyTypes);
            fieldMethodILGenerator.Emit(OpCodes.Ret);
        }

        extras(generatedType);

        var finalType = generatedType.CreateType()!;
        _nameLookup.Add(typeName, finalType);

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
            return GetTypeFromSchemaType(type.OfType);

        if(type.Kind == __TypeKind.LIST)
            return typeof(IEnumerable<>).MakeGenericType(GetTypeFromSchemaType(type.OfType));

        var generateName = GenerateName(type);
        if(_nameLookup.TryGetValue(generateName, out var existingType)) {
            return existingType;
        }

        return GenerateType(type, _ => { });
    }

    private string GenerateName(__Type type) {
        if(type.Kind == __TypeKind.LIST)
            return GenerateName(type.OfType);

        if(type.Kind == __TypeKind.NON_NULL)
            return GenerateName(type.OfType);

        return type.Name;
    }

    public bool ShouldGenerate(__Type type) {
        if(type.Name.StartsWith("__"))
            return false;

        if(type.Name == _federationSchema.QueryType?.Name || type.Name == _federationSchema.MutationType?.Name || type.Name == _federationSchema.SubscriptionType?.Name)
            return false;

        return type.Name is not ("Int" or "Float" or "String" or "Boolean" or "ID");
    }
}