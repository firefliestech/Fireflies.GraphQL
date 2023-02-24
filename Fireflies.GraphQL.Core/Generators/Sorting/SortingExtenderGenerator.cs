using System.Reflection;
using System.Reflection.Emit;
using Fireflies.GraphQL.Abstractions;
using Fireflies.GraphQL.Abstractions.Sorting;
using Fireflies.GraphQL.Core.Extensions;
using GraphQLParser.AST;

namespace Fireflies.GraphQL.Core.Generators.Sorting;

internal class SortingExtenderGenerator : IMethodExtenderGenerator {
    public MethodExtenderDescriptor GetMethodExtenderDescriptor(MemberInfo memberInfo, ref int parameterCount) {
        if(!memberInfo.HasCustomAttribute<GraphQLSortAttribute>())
            return new MethodExtenderDescriptor {
                ShouldDecorate = false
            };

        var baseMethod = memberInfo switch {
            PropertyInfo propertyInfo => propertyInfo.GetMethod!,
            MethodInfo methodInfo => methodInfo,
            _ => throw new ArgumentOutOfRangeException(nameof(memberInfo), memberInfo, null)
        };

        var sortParameterIndex = parameterCount + 1;
        var astNodeParameterIndex = parameterCount + 2;
        var contextParameterIndex = parameterCount + 3;
        parameterCount += 3;

        baseMethod.ReturnType.IsEnumerable(out var returnType);

        var generatedSortType = GenerateSortType(returnType);
        return
            new MethodExtenderDescriptor {
                ShouldDecorate = true,
                ParameterTypes = new[] { generatedSortType, typeof(GraphQLField), typeof(IGraphQLContext) },
                DefineParametersCallback = methodBuilder => {
                    var parameterBuilder = methodBuilder.DefineParameter(sortParameterIndex, ParameterAttributes.HasDefault | ParameterAttributes.Optional, "sort");
                    parameterBuilder.SetConstant(null);
                    parameterBuilder.SetCustomAttribute(new CustomAttributeBuilder(typeof(GraphQLNullable).GetConstructors().First(), Array.Empty<object>()));

                    methodBuilder.DefineParameter(astNodeParameterIndex, ParameterAttributes.HasDefault | ParameterAttributes.Optional, Guid.NewGuid().ToString("N"))
                        .SetCustomAttribute(new CustomAttributeBuilder(typeof(ResolvedAttribute).GetConstructors().First(), Array.Empty<object>()));

                    methodBuilder.DefineParameter(contextParameterIndex, ParameterAttributes.HasDefault | ParameterAttributes.Optional, Guid.NewGuid().ToString("N"))
                        .SetCustomAttribute(new CustomAttributeBuilder(typeof(ResolvedAttribute).GetConstructors().First(), Array.Empty<object>()));
                },
                DecorateCallback = ILGenerator => {
                    baseMethod.ReturnType.IsEnumerable(out var elementType);
                    var isTask = baseMethod.ReturnType.IsGenericType && baseMethod.ReturnType.GetGenericTypeDefinition() == typeof(Task<>);
                    var helperMethodInfo = isTask ? typeof(SortingHelper).GetMethod(nameof(SortingHelper.WrapEnumerableTaskResult), BindingFlags.Public | BindingFlags.Static)! : typeof(SortingHelper).GetMethod(nameof(SortingHelper.WrapEnumerableResult), BindingFlags.Public | BindingFlags.Static)!;

                    helperMethodInfo = helperMethodInfo.MakeGenericMethod(elementType, generatedSortType);

                    ILGenerator.Emit(OpCodes.Ldarg_S, sortParameterIndex);
                    ILGenerator.Emit(OpCodes.Ldarg_S, astNodeParameterIndex);
                    ILGenerator.Emit(OpCodes.Ldarg_S, contextParameterIndex);
                    ILGenerator.EmitCall(OpCodes.Call, helperMethodInfo, null);
                }
            };
    }

    private Type GenerateSortType(Type forType) {
        var sortType = WrapperGenerator.DynamicModule.DefineType(forType.Name + "Sort");

        foreach(var member in forType.GetAllGraphQLMemberInfo()) {
            var subType = member switch {
                PropertyInfo propertyInfo => propertyInfo.PropertyType,
                MethodInfo methodInfo => methodInfo.ReturnType,
                _ => throw new ArgumentOutOfRangeException()
            };

            subType.IsEnumerable(out subType);
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