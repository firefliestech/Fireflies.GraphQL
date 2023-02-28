using System.Reflection;
using System.Reflection.Emit;
using Fireflies.GraphQL.Abstractions;
using Fireflies.GraphQL.Core.Extensions;
using Fireflies.GraphQL.Core.Generators;

namespace Fireflies.GraphQL.Core;

internal class WrapperGenerator {
    private readonly ModuleBuilder _moduleBuilder;
    private readonly GeneratorRegistry _generatorRegistry;
    private readonly Dictionary<Type, Type> _wrappedTypes = new();

    public WrapperGenerator(ModuleBuilder moduleBuilder, GeneratorRegistry generatorRegistry) {
        _moduleBuilder = moduleBuilder;
        _generatorRegistry = generatorRegistry;
    }

    public Type GenerateWrapper(Type baseType, bool copyConstructors = true) {
        if(baseType.HasCustomAttribute<GraphQLNoWrapperAttribute>())
            return baseType;

        if(_wrappedTypes.TryGetValue(baseType, out var existingType)) {
            return existingType;
        }

        var typeBuilder = _moduleBuilder.DefineType($"{baseType.Name}",
            TypeAttributes.Public |
            TypeAttributes.Class |
            TypeAttributes.AutoClass |
            TypeAttributes.AnsiClass |
            TypeAttributes.BeforeFieldInit |
            TypeAttributes.AutoLayout,
            typeof(object));

        var instanceField = typeBuilder.DefineField("_instance", baseType, FieldAttributes.Private);

        foreach(var attribute in baseType.GetCustomAttributesData()) {
            typeBuilder.SetCustomAttribute(attribute.ToAttributeBuilder());
        }

        var createdConstructor = false;
        if(copyConstructors) {
            foreach(var constructor in baseType.GetConstructors(BindingFlags.Public | BindingFlags.Instance)) {
                createdConstructor = true;
                var baseParameters = constructor.GetParameters();
                var constructorBuilder = typeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, baseParameters.Select(p => p.ParameterType).ToArray());
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
        }

        if(!createdConstructor) {
            var constructorBuilder = typeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new[] { baseType });
            var constructorIlGenerator = constructorBuilder.GetILGenerator();
            constructorIlGenerator.Emit(OpCodes.Ldarg_0);
            constructorIlGenerator.Emit(OpCodes.Ldarg_1);
            constructorIlGenerator.Emit(OpCodes.Stfld, instanceField);
            constructorIlGenerator.Emit(OpCodes.Ret);
        }

        var methods = baseType.GetAllGraphQLMethods().Select(x => new { Name = x.Name, MethodInfo = x, Parameters = x.GetParameters(), MemberInfo = x as MemberInfo });
        var properties = baseType.GetAllGraphQLProperties().Select(x => new { Name = x.Name, MethodInfo = x.GetMethod!, Parameters = Array.Empty<ParameterInfo>(), MemberInfo = x as MemberInfo });
        foreach(var baseMethod in methods.Union(properties)) {
            var (wrappedReturnType, isEnumerable, originalType, wrapperType) = GetWrappedReturnType(baseMethod.MethodInfo);

            var parameterTypes = baseMethod.Parameters.Select(x => x.ParameterType);
            var parameterCount = baseMethod.Parameters.Length;

            var decoratorDescriptors = _generatorRegistry.GetGenerators<IMethodExtenderGenerator>()
                .Select(m => m.GetMethodExtenderDescriptor(baseMethod.MemberInfo, wrappedReturnType, ref parameterCount))
                .Where(x => x.ShouldDecorate).ToArray();
            parameterTypes = parameterTypes.Union(decoratorDescriptors.SelectMany(x => x.ParameterTypes));

            var methodBuilder = typeBuilder.DefineMethod(baseMethod.Name, MethodAttributes.Public, CallingConventions.Standard, wrappedReturnType, parameterTypes.ToArray());
            baseMethod.MemberInfo.CopyAttributes(ab => methodBuilder.SetCustomAttribute(ab));
            methodBuilder.DefineParameters(baseMethod.Parameters);

            foreach(var middlewareParameter in decoratorDescriptors)
                middlewareParameter.DefineParametersCallback(methodBuilder);

            var methodIlGenerator = methodBuilder.GetILGenerator();
            methodIlGenerator.Emit(OpCodes.Ldarg_0);
            methodIlGenerator.Emit(OpCodes.Ldfld, instanceField); // Load wrapped object from this
            for(var i = 1; i <= baseMethod.Parameters.Length; i++)
                methodIlGenerator.Emit(OpCodes.Ldarg_S, i++); // Load base arguments
            methodIlGenerator.EmitCall(OpCodes.Call, baseMethod.MethodInfo, null); // Call method on wrapped object with the base arguments

            // If the returned value a wrapper it needs to be converted from original return type
            GenerateResultConverter(baseMethod.MethodInfo.ReturnType, wrapperType, originalType, methodIlGenerator);

            // Call method middlewares passing result from previous operation
            foreach(var decorator in decoratorDescriptors)
                decorator.DecorateCallback(methodIlGenerator);

            methodIlGenerator.Emit(OpCodes.Ret); // Return

            foreach(var typeExtender in _generatorRegistry.GetGenerators<ITypeExtenderGenerator>())
                typeExtender.Extend(typeBuilder, methodBuilder, baseMethod.MemberInfo, instanceField, decoratorDescriptors);
        }

        var createdType = typeBuilder.CreateType()!;
        _wrappedTypes.Add(baseType, createdType);
        return createdType;
    }

    private void GenerateResultConverter(Type returnType, Type wrapperType, Type originalType, ILGenerator methodIlGenerator) {
        var wrapperHelperType = typeof(WrapperHelper);

        MethodInfo? methodInfo = null;
        if(returnType.IsGenericType) {
            if(returnType.IsTask()) {
                if(returnType.IsEnumerable()) {
                    methodInfo = wrapperHelperType.GetMethod(nameof(WrapperHelper.WrapEnumerableTaskResult), BindingFlags.Public | BindingFlags.Static)!;
                } else if(returnType.IsQueryable()) {
                    methodInfo = wrapperHelperType.GetMethod(nameof(WrapperHelper.WrapQueryableTaskResult), BindingFlags.Public | BindingFlags.Static)!;
                } else {
                    methodInfo = wrapperHelperType.GetMethod(nameof(WrapperHelper.WrapTaskResult), BindingFlags.Public | BindingFlags.Static)!;
                }
            } else if(returnType.IsAsyncEnumerable()) {
                methodInfo = wrapperHelperType.GetMethod(nameof(WrapperHelper.WrapAsyncEnumerableResult), BindingFlags.Public | BindingFlags.Static)!;
                methodIlGenerator.DeclareLocal(typeof(CancellationToken));
                methodIlGenerator.Emit(OpCodes.Ldloc_0);
            }
        }

        if(methodInfo == null) {
            if(returnType.IsEnumerable()) {
                methodInfo = wrapperHelperType.GetMethod(nameof(WrapperHelper.WrapEnumerableResult), BindingFlags.Public | BindingFlags.Static)!;
            } else if(returnType.IsQueryable()) {
                methodInfo = wrapperHelperType.GetMethod(nameof(WrapperHelper.WrapQueryableResult), BindingFlags.Public | BindingFlags.Static)!;
            } else {
                methodInfo = wrapperHelperType.GetMethod(nameof(WrapperHelper.WrapResult), BindingFlags.Public | BindingFlags.Static)!;
            }
        }

        methodInfo = methodInfo.MakeGenericMethod(wrapperType, originalType);
        methodIlGenerator.EmitCall(OpCodes.Call, methodInfo, null);
    }

    private (Type, bool, Type, Type) GetWrappedReturnType(MethodInfo methodInfo) {
        var isEnumerable = methodInfo.ReturnType.IsCollection(out var elementType);
        if(elementType.HasCustomAttribute<GraphQLNoWrapperAttribute>() || elementType.IsValueType || elementType == typeof(string) || elementType.IsInterface) {
            return isEnumerable ?
                CreateReturnType(methodInfo, elementType, elementType, isEnumerable) :
                (methodInfo.ReturnType, isEnumerable, elementType, elementType);
        }

        var wrapperType = GenerateWrapper(elementType, false);
        return CreateReturnType(methodInfo, wrapperType, elementType, isEnumerable);
    }

    private static (Type, bool, Type, Type) CreateReturnType(MethodInfo methodInfo, Type wrapperType, Type elementType, bool isEnumerable) {
        if(methodInfo.ReturnType.IsAsyncEnumerable()) {
            return (typeof(IAsyncEnumerable<>).MakeGenericType(wrapperType), true, elementType, wrapperType);
        }

        if(isEnumerable) {
            return methodInfo.ReturnType.IsTask() ?
                (typeof(Task<>).MakeGenericType(typeof(IQueryable<>).MakeGenericType(wrapperType)), true, elementType, wrapperType) :
                (typeof(IQueryable<>).MakeGenericType(wrapperType), true, elementType, wrapperType);
        }

        return methodInfo.ReturnType.IsTask() ?
            (typeof(Task<>).MakeGenericType(wrapperType), false, elementType, wrapperType) :
            (wrapperType, false, elementType, wrapperType);
    }
}