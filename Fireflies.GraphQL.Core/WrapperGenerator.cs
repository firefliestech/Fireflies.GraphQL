using System.Reflection;
using System.Reflection.Emit;
using Fireflies.GraphQL.Abstractions;
using Fireflies.GraphQL.Abstractions.Generator;
using Fireflies.GraphQL.Core.Exceptions;
using Fireflies.GraphQL.Core.Extensions;
using Fireflies.GraphQL.Core.Generators;
using Fireflies.Utility.Reflection;

namespace Fireflies.GraphQL.Core;

internal class WrapperGenerator {
    private readonly ModuleBuilder _moduleBuilder;
    private readonly GeneratorRegistry _generatorRegistry;
    private readonly WrapperRegistry _wrapperRegistry;

    public WrapperGenerator(ModuleBuilder moduleBuilder, GeneratorRegistry generatorRegistry, WrapperRegistry wrapperRegistry) {
        _moduleBuilder = moduleBuilder;
        _generatorRegistry = generatorRegistry;
        _wrapperRegistry = wrapperRegistry;
    }

    public Type GenerateWrapper(Type baseType, bool copyConstructors = true) {
        try {
            return InternalGenerateWrapper(baseType, copyConstructors);
        } catch(Exception ex) {
            throw new GraphQLException($"Error while generating wrapper for {baseType.Name}", ex);
        }
    }

    private Type InternalGenerateWrapper(Type baseType, bool copyConstructors) {
        if(baseType.HasCustomAttribute<GraphQLNoWrapperAttribute>() || baseType.IsFrameworkType())
            return baseType;

        if(_wrapperRegistry.TryGetValue(baseType, out var existingType))
            return existingType;

        var isInterface = baseType.IsInterface;

        var typeBuilder = _moduleBuilder.DefineType($"{baseType.Name}",
            isInterface ? TypeAttributes.Interface | TypeAttributes.Public | TypeAttributes.Abstract : TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.AutoClass | TypeAttributes.AnsiClass | TypeAttributes.BeforeFieldInit | TypeAttributes.AutoLayout,
            isInterface ? null : typeof(object));
        _wrapperRegistry.Add(baseType, typeBuilder);

        baseType.CopyAttributes(a => typeBuilder.SetCustomAttribute(a));

        if(!isInterface) {
            foreach(var interf in baseType.GetInterfaces()) {
                var wrappedInterfaceType = GenerateWrapper(interf);
                typeBuilder.AddInterfaceImplementation(wrappedInterfaceType);
            }
        }

        FieldBuilder instanceField = null!;
        if(!isInterface) {
            instanceField = typeBuilder.DefineField("_instance", baseType, FieldAttributes.Private);
            CreateConstructor(baseType, copyConstructors, typeBuilder, instanceField);
        }

        var methods = baseType.GetAllGraphQLMethods().Select(x => new { Name = x.Name, MethodInfo = x, Parameters = x.GetParameters(), MemberInfo = x as MemberInfo });
        var properties = baseType.GetAllGraphQLProperties().Select(x => new { Name = x.Name, MethodInfo = x.GetMethod!, Parameters = Array.Empty<ParameterInfo>(), MemberInfo = x as MemberInfo });
        foreach(var baseMethod in methods.Concat(properties)) {
            var (wrappedReturnType, originalType, wrapperType) = GetWrappedReturnType(baseMethod.MethodInfo, baseMethod.MemberInfo);

            var parameterTypes = baseMethod.Parameters.Select(x => x.ParameterType);
            var parameterCount = baseMethod.Parameters.Length;

            parameterTypes = parameterTypes.Concat(new[] { typeof(WrapperRegistry) });
            parameterCount += 1;
            var wrapperRegistryIndex = parameterCount;

            var decoratorDescriptors = _generatorRegistry.GetGenerators<IMethodExtenderGenerator>()
                .Select(m => m.GetMethodExtenderDescriptor(baseMethod.MemberInfo, originalType, wrappedReturnType, ref parameterCount))
                .Where(x => x.ShouldDecorate).ToArray();
            parameterTypes = parameterTypes.Concat(decoratorDescriptors.SelectMany(x => x.ParameterTypes));

            var methodBuilder = typeBuilder.DefineMethod(baseMethod.Name,
                isInterface ? MethodAttributes.Abstract | MethodAttributes.Public | MethodAttributes.Virtual : MethodAttributes.Public | MethodAttributes.Virtual,
                CallingConventions.Standard, wrappedReturnType, parameterTypes.ToArray());
            baseMethod.MemberInfo.CopyAttributes(ab => methodBuilder.SetCustomAttribute(ab));

            if(NullabilityChecker.IsNullable(baseMethod.MethodInfo))
                methodBuilder.SetCustomAttribute(new CustomAttributeBuilder(typeof(GraphQLNullable).GetConstructors().First(), Array.Empty<object>()));

            methodBuilder.DefineParameters(baseMethod.Parameters);
            methodBuilder.DefineAnonymousResolvedParameter(wrapperRegistryIndex);

            foreach(var middlewareParameter in decoratorDescriptors)
                middlewareParameter.DefineParametersCallback(methodBuilder);

            if(!isInterface) {
                foreach(var interf in baseType.GetInterfaces()) {
                    var wrappedInterfaceType = GenerateWrapper(interf);
                    var overridingMember = wrappedInterfaceType.GetMethod(baseMethod.Name, BindingFlags.Public | BindingFlags.Instance);
                    if(overridingMember != null) {
                        typeBuilder.DefineMethodOverride(methodBuilder, overridingMember);
                        break;
                    }
                }

                var methodIlGenerator = methodBuilder.GetILGenerator();
                methodIlGenerator.Emit(OpCodes.Ldarg_0);
                methodIlGenerator.Emit(OpCodes.Ldfld, instanceField); // Load wrapped object from this
                for(var i = 1; i <= baseMethod.Parameters.Length; i++)
                    methodIlGenerator.Emit(OpCodes.Ldarg_S, i); // Load base arguments
                methodIlGenerator.EmitCall(OpCodes.Call, baseMethod.MethodInfo, null); // Call method on wrapped object with the base arguments

                // Call method middlewares passing result from previous operation
                foreach(var decorator in decoratorDescriptors)
                    decorator.GenerateCallback(MethodExtenderStep.BeforeWrap, methodIlGenerator);

                // If the returned value a wrapper it needs to be converted from original return type
                GenerateResultConverter(baseMethod.MethodInfo.ReturnType, wrapperType, originalType, methodIlGenerator, wrapperRegistryIndex);

                // Call method middlewares passing result from previous operation
                foreach(var decorator in decoratorDescriptors)
                    decorator.GenerateCallback(MethodExtenderStep.AfterWrap, methodIlGenerator);

                methodIlGenerator.Emit(OpCodes.Ret); // Return
            }

            var baseDescriptor = new BaseDescriptor() {
                ReturnType = baseMethod.MethodInfo.ReturnType,
                MemberInfo = baseMethod.MemberInfo,
                ParameterTypes = parameterTypes,
                GeneratingInterface = isInterface,
                DefineParameterCallbacks = new Action<MethodBuilder>[] {
                    mb => mb.DefineParameters(baseMethod.Parameters),
                    mb => mb.DefineAnonymousResolvedParameter(wrapperRegistryIndex)
                }.Union(decoratorDescriptors.Select(x => x.DefineParametersCallback))
            };

            foreach(var typeExtender in _generatorRegistry.GetGenerators<ITypeExtenderGenerator>())
                typeExtender.Extend(typeBuilder, methodBuilder, baseDescriptor);
        }

        var createdType = typeBuilder.CreateType()!;
        _wrapperRegistry.Add(baseType, createdType);
        return createdType;
    }

    private static void CreateConstructor(Type baseType, bool copyConstructors, TypeBuilder typeBuilder, FieldBuilder instanceField) {
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
    }

    private void GenerateResultConverter(Type returnType, Type wrapperType, Type originalType, ILGenerator methodIlGenerator, int wrapperRegistryIndex) {
        var wrapperHelperType = typeof(WrapperHelper);

        MethodInfo? methodInfo = null;
        if(returnType.IsGenericType) {
            if(returnType.IsTask()) {
                if(returnType.IsQueryable()) {
                    methodInfo = wrapperHelperType.GetMethod(nameof(WrapperHelper.WrapQueryableTaskResult), BindingFlags.Public | BindingFlags.Static)!;
                } else if(returnType.IsCollection()) {
                    methodInfo = wrapperHelperType.GetMethod(nameof(WrapperHelper.WrapEnumerableTaskResult), BindingFlags.Public | BindingFlags.Static)!;
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
            if(returnType.IsQueryable()) {
                methodInfo = wrapperHelperType.GetMethod(nameof(WrapperHelper.WrapQueryableResult), BindingFlags.Public | BindingFlags.Static)!;
            } else if(returnType.IsCollection()) {
                methodInfo = wrapperHelperType.GetMethod(nameof(WrapperHelper.WrapEnumerableResult), BindingFlags.Public | BindingFlags.Static)!;
            } else {
                methodInfo = wrapperHelperType.GetMethod(nameof(WrapperHelper.WrapResult), BindingFlags.Public | BindingFlags.Static)!;
            }
        }

        methodInfo = methodInfo.MakeGenericMethod(wrapperType, originalType);
        methodIlGenerator.Emit(OpCodes.Ldarg_S, wrapperRegistryIndex);
        methodIlGenerator.EmitCall(OpCodes.Call, methodInfo, null);
    }

    private (Type, Type, Type) GetWrappedReturnType(MethodInfo methodInfo, MemberInfo memberInfo) {
        var isEnumerable = methodInfo.ReturnType.IsCollection(out var elementType);
        if(elementType.IsInterface) {
            var interfaceType = GenerateWrapper(elementType);

            foreach(var impl in ReflectionCache.GetAllClassesThatImplements(elementType, false)) {
                GenerateWrapper(impl, false);
            }

            return CreateReturnType(methodInfo, interfaceType, elementType);
        }

        if(memberInfo.HasCustomAttribute<GraphQLIdAttribute>(out var x) && !x!.KeepAsOriginalType) {
            var graphQlId = typeof(GraphQLId<>).MakeGenericType(elementType);
            return CreateReturnType(methodInfo, graphQlId, elementType);
        }

        if(elementType.HasCustomAttribute<GraphQLNoWrapperAttribute>() || elementType.IsValueType || elementType == typeof(string)) {
            return isEnumerable ? CreateReturnType(methodInfo, elementType, elementType) : (methodInfo.ReturnType, elementType, elementType);
        }

        var wrapperType = GenerateWrapper(elementType, false);
        return CreateReturnType(methodInfo, wrapperType, elementType);
    }

    private static (Type, Type, Type) CreateReturnType(MethodInfo methodInfo, Type wrapperType, Type elementType) {
        if(methodInfo.ReturnType.IsAsyncEnumerable()) {
            return (typeof(IAsyncEnumerable<>).MakeGenericType(wrapperType), elementType, wrapperType);
        }

        if(methodInfo.ReturnType.IsQueryable())
            return methodInfo.ReturnType.IsTask() ? (typeof(Task<>).MakeGenericType(typeof(IQueryable<>).MakeGenericType(wrapperType)), elementType, wrapperType) : (typeof(IQueryable<>).MakeGenericType(wrapperType), elementType, wrapperType);

        if(methodInfo.ReturnType.IsCollection())
            return methodInfo.ReturnType.IsTask() ? (typeof(Task<>).MakeGenericType(typeof(IEnumerable<>).MakeGenericType(wrapperType)), elementType, wrapperType) : (typeof(IEnumerable<>).MakeGenericType(wrapperType), elementType, wrapperType);

        return methodInfo.ReturnType.IsTask() ? (typeof(Task<>).MakeGenericType(wrapperType), elementType, wrapperType) : (wrapperType, elementType, wrapperType);
    }
}