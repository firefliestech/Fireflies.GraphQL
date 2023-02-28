using System.Reflection;
using System.Reflection.Emit;
using Fireflies.GraphQL.Abstractions;
using Fireflies.GraphQL.Abstractions.Sorting;
using Fireflies.GraphQL.Core.Extensions;
using GraphQLParser.AST;

namespace Fireflies.GraphQL.Core.Generators.Sorting;

internal class SortingGenerator : IMethodExtenderGenerator {
    private readonly ModuleBuilder _moduleBuilder;

    public SortingGenerator(ModuleBuilder moduleBuilder) {
        _moduleBuilder = moduleBuilder;
    }

    public MethodExtenderDescriptor GetMethodExtenderDescriptor(MemberInfo memberInfo, Type wrappedReturnType, ref int parameterCount) {
        if(!memberInfo.HasCustomAttribute<GraphQLSortAttribute>())
            return new MethodExtenderDescriptor();

        var sortParameterIndex = parameterCount + 1;
        var astNodeParameterIndex = parameterCount + 2;
        var contextParameterIndex = parameterCount + 3;
        parameterCount += 3;

        wrappedReturnType.IsCollection(out var returnType);

        var generatedSortType = GenerateSortType(returnType);
        return
            new MethodExtenderDescriptor(
                new[] { generatedSortType, typeof(GraphQLField), typeof(IGraphQLContext) },
                methodBuilder => {
                    var parameterBuilder = methodBuilder.DefineParameter(sortParameterIndex, ParameterAttributes.HasDefault | ParameterAttributes.Optional, "sort");
                    parameterBuilder.SetConstant(null);
                    parameterBuilder.SetCustomAttribute(new CustomAttributeBuilder(typeof(GraphQLNullable).GetConstructors().First(), Array.Empty<object>()));

                    methodBuilder.DefineParameter(astNodeParameterIndex, ParameterAttributes.HasDefault | ParameterAttributes.Optional, Guid.NewGuid().ToString("N"))
                        .SetCustomAttribute(new CustomAttributeBuilder(typeof(ResolvedAttribute).GetConstructors().First(), Array.Empty<object>()));

                    methodBuilder.DefineParameter(contextParameterIndex, ParameterAttributes.HasDefault | ParameterAttributes.Optional, Guid.NewGuid().ToString("N"))
                        .SetCustomAttribute(new CustomAttributeBuilder(typeof(ResolvedAttribute).GetConstructors().First(), Array.Empty<object>()));
                },
                ilGenerator => {
                    wrappedReturnType.IsCollection(out var elementType);
                    var helperMethodInfo = wrappedReturnType.IsTask() ?
                        typeof(SortingHelper).GetMethod(nameof(SortingHelper.WrapEnumerableTaskResult), BindingFlags.Public | BindingFlags.Static)! :
                        typeof(SortingHelper).GetMethod(nameof(SortingHelper.WrapEnumerableResult), BindingFlags.Public | BindingFlags.Static)!;

                    helperMethodInfo = helperMethodInfo.MakeGenericMethod(elementType, generatedSortType);

                    ilGenerator.Emit(OpCodes.Ldarg_S, sortParameterIndex);
                    ilGenerator.Emit(OpCodes.Ldarg_S, astNodeParameterIndex);
                    ilGenerator.Emit(OpCodes.Ldarg_S, contextParameterIndex);
                    ilGenerator.EmitCall(OpCodes.Call, helperMethodInfo, null);
                }
            );
    }

    private Type GenerateSortType(Type forType) {
        var sortType = _moduleBuilder.DefineType(forType.Name + "Sort");

        foreach(var member in forType.GetAllGraphQLMemberInfo()) {
            var subType = member switch {
                PropertyInfo propertyInfo => propertyInfo.PropertyType,
                MethodInfo methodInfo => methodInfo.ReturnType,
                _ => throw new ArgumentOutOfRangeException()
            };

            subType.IsCollection(out subType);
            if(!subType.IsValidGraphQLObject()) {
                DefineSortProperty(typeof(SortOrder?), sortType, member);
            }
        }

        return sortType.CreateType()!;
    }

    private static void DefineSortProperty(Type propertyType, TypeBuilder sortType, MemberInfo member) {
        var field = sortType.DefineField("_" + member.Name.LowerCaseFirstLetter(), propertyType, FieldAttributes.Private);

        var property = sortType.DefineProperty(member.Name, PropertyAttributes.HasDefault, CallingConventions.Standard, propertyType, Type.EmptyTypes);
        property.SetCustomAttribute(new CustomAttributeBuilder(typeof(GraphQLNullable).GetConstructors().First(), Array.Empty<object>()));

        var getMethod = sortType.DefineMethod($"get_{member.Name}", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.Virtual, CallingConventions.Standard, propertyType, Type.EmptyTypes);

        property.SetGetMethod(getMethod);
        var getILGenerator = getMethod.GetILGenerator();
        getILGenerator.Emit(OpCodes.Ldarg_0);
        getILGenerator.Emit(OpCodes.Ldfld, field);
        getILGenerator.Emit(OpCodes.Ret);

        var setMethod = sortType.DefineMethod($"set_{member.Name}", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.Virtual, CallingConventions.Standard, typeof(void), new[] { propertyType });

        var setILGenerator = setMethod.GetILGenerator();
        setILGenerator.Emit(OpCodes.Ldarg_0);
        setILGenerator.Emit(OpCodes.Ldarg_1);
        setILGenerator.Emit(OpCodes.Stfld, field);
        setILGenerator.Emit(OpCodes.Ret);

        property.SetSetMethod(setMethod);
    }
}