using System.Reflection;
using System.Reflection.Emit;
using Fireflies.GraphQL.Abstractions;
using Fireflies.GraphQL.Core.Extensions;
using Fireflies.GraphQL.Core.Pagination;

namespace Fireflies.GraphQL.Core;

internal static class WrapperGenerator {
    internal static ModuleBuilder DynamicModule { get; }

    private static readonly Dictionary<Type, Type> WrappedTypes = new();

    static WrapperGenerator() {
        var assemblyName = new AssemblyName("Fireflies.GraphQL.ProxyAssembly");
        var dynamicAssembly = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        DynamicModule = dynamicAssembly.DefineDynamicModule("Main");
    }

    public static Type GenerateWrapper(Type baseType, bool copyConstructors = true) {
        if(WrappedTypes.TryGetValue(baseType, out var existingType)) {
            return existingType;
        }

        var wrappedType = DynamicModule.DefineType($"{baseType.Name}",
            TypeAttributes.Public |
            TypeAttributes.Class |
            TypeAttributes.AutoClass |
            TypeAttributes.AnsiClass |
            TypeAttributes.BeforeFieldInit |
            TypeAttributes.AutoLayout,
            typeof(object));

        var instanceField = wrappedType.DefineField("_instance", baseType, FieldAttributes.Private);

        foreach(var attribute in baseType.GetCustomAttributesData()) {
            wrappedType.SetCustomAttribute(attribute.ToAttributeBuilder());
        }

        if(copyConstructors) {
            foreach(var constructor in baseType.GetConstructors(BindingFlags.Public | BindingFlags.Instance)) {
                var baseParameters = constructor.GetParameters();
                var constructorBuilder = wrappedType.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, baseParameters.Select(p => p.ParameterType).ToArray());
                constructorBuilder.DefineParameters(baseParameters);

                var constructorIlGenerator = constructorBuilder.GetILGenerator();
                constructorIlGenerator.Emit(OpCodes.Ldarg_0);
                var i = 1;
                foreach(var _ in baseParameters) {
                    constructorIlGenerator.Emit(OpCodes.Ldarg_S, i++);
                }

                constructorIlGenerator.Emit(OpCodes.Newobj, constructor);
                constructorIlGenerator.Emit(OpCodes.Stfld, instanceField);
                constructorIlGenerator.Emit(OpCodes.Ret);
            }
        } else {
            var constructorBuilder = wrappedType.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new[] { baseType });
            var constructorIlGenerator = constructorBuilder.GetILGenerator();
            constructorIlGenerator.Emit(OpCodes.Ldarg_0);
            constructorIlGenerator.Emit(OpCodes.Ldarg_1);
            constructorIlGenerator.Emit(OpCodes.Stfld, instanceField);
            constructorIlGenerator.Emit(OpCodes.Ret);
        }

        foreach(var baseMethod in baseType.GetAllGraphQLMethods()) {
            var baseMethodParameters = baseMethod.GetParameters();
            var (wrappedReturnType, isEnumerable, originalType, wrapperType) = GetWrappedReturnType(baseMethod);

            var methodBuilder = wrappedType.DefineMethod(baseMethod.Name, MethodAttributes.Public, CallingConventions.Standard, wrappedReturnType, baseMethodParameters.Select(x => x.ParameterType).ToArray());
            var needsConversion = originalType != wrapperType;
            WrapMethod(methodBuilder, baseMethodParameters, baseMethod, instanceField, isEnumerable, wrapperType, originalType, needsConversion);
            CopyAttributes(baseMethod, ab => methodBuilder.SetCustomAttribute(ab));

            if(baseMethod.HasCustomAttribute<GraphQlPaginationAttribute>()) {
                AddConnectionMethod(wrappedType, baseMethod.Name, baseMethod, instanceField);
            }
        }

        IEnumerable<(PropertyInfo PropertyInfo, Type? DeclaringType)> propertiesToWrap = baseType.GetAllGraphQLProperties().Select(property => (property, (Type?)null));
        if(baseType.GetInterfaces().Contains(typeof(IASTNodeHandler))) {
            wrappedType.AddInterfaceImplementation(typeof(IASTNodeHandler));
            propertiesToWrap = propertiesToWrap.Union(new[] { (baseType.GetProperty("ASTNode")!, (Type?)typeof(IASTNodeHandler)) });
        }

        foreach(var entry in propertiesToWrap) {
            var baseProperty = entry.PropertyInfo;
            var propertyBuilder = wrappedType.DefineProperty(baseProperty.Name, PropertyAttributes.None, baseProperty.PropertyType, Type.EmptyTypes);
            CopyAttributes(baseProperty, ab => propertyBuilder.SetCustomAttribute(ab));

            if(baseProperty.GetMethod != null) {
                var baseGetParameters = baseProperty.GetMethod!.GetParameters();
                var (wrappedReturnType, isEnumerable, originalType, wrapperType) = GetWrappedReturnType(baseProperty.GetMethod!);
                var getterMethodBuilder = wrappedType.DefineMethod("get_" + baseProperty.Name, MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.Virtual, CallingConventions.Standard, baseProperty.PropertyType, baseGetParameters.Select(x => x.ParameterType).ToArray());
                if(entry.DeclaringType != null) {
                    wrappedType.DefineMethodOverride(getterMethodBuilder, entry.DeclaringType.GetMethod($"get_{baseProperty.Name}")!);
                }

                var needsConversion = originalType != wrapperType;
                WrapMethod(getterMethodBuilder, baseGetParameters, baseProperty.GetMethod!, instanceField, isEnumerable, wrapperType, originalType, needsConversion);
                propertyBuilder.SetGetMethod(getterMethodBuilder);
            }

            if(entry.DeclaringType != null && baseProperty.SetMethod != null) { // Only do setters for interfaces
                var baseSetParameters = baseProperty.SetMethod?.GetParameters();
                var setterMethodBuilder = wrappedType.DefineMethod("set_" + baseProperty.Name, MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.Virtual, CallingConventions.Standard, typeof(void), baseSetParameters.Select(x => x.ParameterType).ToArray());
                wrappedType.DefineMethodOverride(setterMethodBuilder, entry.DeclaringType.GetMethod($"set_{baseProperty.Name}")!);
                WrapMethod(setterMethodBuilder, baseSetParameters, baseProperty.SetMethod!, instanceField, false, typeof(void), typeof(void), false);
                propertyBuilder.SetSetMethod(setterMethodBuilder);
            }

            if(baseProperty.HasCustomAttribute<GraphQlPaginationAttribute>()) {
                AddConnectionMethod(wrappedType, baseProperty.Name, baseProperty.GetMethod!, instanceField);
            }
        }

        var finalType = wrappedType.CreateType()!;
        WrappedTypes.Add(baseType, finalType);
        return finalType;
    }

    private static void AddConnectionMethod(TypeBuilder targetType, string name, MethodInfo baseMethod, FieldInfo instanceField) {
        var (_, isEnumerable, originalType, wrapperType) = GetWrappedReturnType(baseMethod);

        if(!isEnumerable)
            throw new GraphQLTypeException($"Cant add pagination for {name} because return type is not IEnumerable");

        if(!originalType.GetProperties(BindingFlags.Public | BindingFlags.Instance).Any(x => x.HasCustomAttribute<GraphQlIdAttribute>())) {
            throw new GraphQLTypeException($"Cant add pagination for {originalType} because return type does not have any {nameof(GraphQlIdAttribute)} attributes");
        }

        var connectorTypeName = $"{name}Connection";
        var (connectionType, edgeType) = GenerateConnectionType(connectorTypeName, wrapperType);
        var methodParameters = new List<Type>();
        var baseParameters = baseMethod.GetParameters();
        methodParameters.AddRange(baseParameters.Select(x => x.ParameterType));
        methodParameters.Add(typeof(int));
        methodParameters.Add(typeof(string));

        var returnType = typeof(Task<>).MakeGenericType(connectionType);
        var methodBuilder = targetType.DefineMethod(connectorTypeName,
            MethodAttributes.Public,
            CallingConventions.Standard,
            returnType,
            methodParameters.ToArray());
        CopyAttributes(baseMethod, ab => methodBuilder.SetCustomAttribute(ab));

        methodBuilder.DefineParameters(baseParameters);
        methodBuilder.DefineParameter(baseParameters.Length + 1, ParameterAttributes.HasDefault, "first").SetConstant(10);
        methodBuilder.DefineParameter(baseParameters.Length + 2, ParameterAttributes.HasDefault, "after").SetConstant(null);

        var methodIlGenerator = methodBuilder.GetILGenerator();

        methodIlGenerator.Emit(OpCodes.Ldarg_0);
        methodIlGenerator.Emit(OpCodes.Ldfld, instanceField);

        for(var i = 1; i <= baseParameters.Length; i++) {
            methodIlGenerator.Emit(OpCodes.Ldarg_S, i);
        }

        methodIlGenerator.EmitCall(OpCodes.Call, baseMethod, null);

        var needsConversion = originalType != wrapperType;
        var isTask = baseMethod.ReturnType.IsGenericType && baseMethod.ReturnType.GetGenericTypeDefinition() == typeof(Task<>);

        if(needsConversion) {
            ConvertToWrapper(baseMethod, isEnumerable, wrapperType, originalType, methodIlGenerator);
        }

        if(!isTask) {
            var taskFromResultMethod = typeof(Task).GetMethod(nameof(Task.FromResult), BindingFlags.Public | BindingFlags.Static)!.MakeGenericMethod(connectionType);
            methodIlGenerator.EmitCall(OpCodes.Call, taskFromResultMethod, null);
        }

        methodIlGenerator.Emit(OpCodes.Ldarg_S, baseParameters.Length + 1); // first
        methodIlGenerator.Emit(OpCodes.Ldarg_S, baseParameters.Length + 2); // after

        var createConnectionMethod = typeof(WrapperHelper).GetMethod(nameof(WrapperHelper.CreateEnumerableTaskConnection), BindingFlags.Public | BindingFlags.Static)!.MakeGenericMethod(connectionType, edgeType, wrapperType);
        methodIlGenerator.EmitCall(OpCodes.Call, createConnectionMethod, null);

        methodIlGenerator.Emit(OpCodes.Ret);
    }

    private static void WrapMethod(MethodBuilder methodBuilder, ParameterInfo[] baseMethodParameters, MethodInfo baseMethod, FieldInfo instanceField, bool isEnumerable, Type wrapperType, Type originalType, bool needsConversion) {
        methodBuilder.DefineParameters(baseMethodParameters);
        var methodIlGenerator = methodBuilder.GetILGenerator();

        methodIlGenerator.Emit(OpCodes.Ldarg_0);
        methodIlGenerator.Emit(OpCodes.Ldfld, instanceField);
        var i = 1;
        foreach(var _ in baseMethodParameters) {
            methodIlGenerator.Emit(OpCodes.Ldarg_S, i++);
        }

        methodIlGenerator.EmitCall(OpCodes.Call, baseMethod, null);

        if(needsConversion) {
            ConvertToWrapper(baseMethod, isEnumerable, wrapperType, originalType, methodIlGenerator);
        }

        methodIlGenerator.Emit(OpCodes.Ret);
    }

    private static void ConvertToWrapper(MethodInfo baseMethod, bool isEnumerable, Type wrapperType, Type originalType, ILGenerator methodIlGenerator) {
        var isTask = baseMethod.ReturnType.IsGenericType && baseMethod.ReturnType.GetGenericTypeDefinition() == typeof(Task<>);
        MethodInfo methodInfo;
        if(isTask && isEnumerable) {
            methodInfo = typeof(WrapperHelper).GetMethod(nameof(WrapperHelper.WrapEnumerableTaskResult), BindingFlags.Public | BindingFlags.Static)!;
        } else if(isTask) {
            methodInfo = typeof(WrapperHelper).GetMethod(nameof(WrapperHelper.WrapTaskResult), BindingFlags.Public | BindingFlags.Static)!;
        } else if(isEnumerable) {
            methodInfo = typeof(WrapperHelper).GetMethod(nameof(WrapperHelper.WrapEnumerableResult), BindingFlags.Public | BindingFlags.Static)!;
        } else {
            methodInfo = typeof(WrapperHelper).GetMethod(nameof(WrapperHelper.WrapResult), BindingFlags.Public | BindingFlags.Static)!;
        }

        methodInfo = methodInfo.MakeGenericMethod(wrapperType, originalType);
        methodIlGenerator.EmitCall(OpCodes.Call, methodInfo, null);
    }

    private static (Type, bool, Type, Type) GetWrappedReturnType(MethodInfo methodInfo) {
        var isEnumerable = methodInfo.ReturnType.IsEnumerable(out var elementType);
        var anyMemberNeedsWrapper = elementType.GetMembers().Any(x => x.HasCustomAttribute<GraphQlPaginationAttribute>());
        if(!anyMemberNeedsWrapper || methodInfo.ReturnType.IsValueType || methodInfo.ReturnType == typeof(string)) {
            return (methodInfo.ReturnType, isEnumerable, elementType, elementType);
        }

        var isTask = methodInfo.ReturnType.IsGenericType && methodInfo.ReturnType.GetGenericTypeDefinition() == typeof(Task<>);
        var wrapperType = GenerateWrapper(elementType, false);
        if(isEnumerable) {
            return isTask
                ? (typeof(Task<>).MakeGenericType(typeof(IEnumerable<>).MakeGenericType(wrapperType)), true, elementType, wrapperType)
                : (typeof(IEnumerable<>).MakeGenericType(wrapperType), true, elementType, wrapperType);
        }

        return isTask ? (typeof(Task<>).MakeGenericType(wrapperType), false, elementType, wrapperType) : (wrapperType, false, elementType, wrapperType);
    }

    private static (Type, Type) GenerateConnectionType(string typeName, Type nodeType) {
        var edgeType = GenerateEdgeType(nodeType);
        var baseType = typeof(ConnectionBase<,>).MakeGenericType(edgeType, nodeType);
        var connectionType = DynamicModule.DefineType(typeName,
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

    private static Type GenerateEdgeType(Type nodeType) {
        var baseType = typeof(EdgeBase<>).MakeGenericType(nodeType);
        var edgeType = DynamicModule.DefineType($"{nodeType.Name}Edge",
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

    private static void CopyAttributes(MemberInfo copyFrom, Action<CustomAttributeBuilder> callback) {
        foreach(var customAttribute in copyFrom.GetCustomAttributesData()) {
            callback(customAttribute.ToAttributeBuilder());
        }
    }
}