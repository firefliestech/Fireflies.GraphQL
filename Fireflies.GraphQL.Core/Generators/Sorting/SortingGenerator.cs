using System.Reflection;
using System.Reflection.Emit;
using Fireflies.GraphQL.Abstractions.Generator;
using Fireflies.GraphQL.Abstractions.Sorting;
using Fireflies.GraphQL.Core.Extensions;
using Fireflies.GraphQL.Core.Scalar;
using Fireflies.Utility.Reflection;
using GraphQLParser.AST;

namespace Fireflies.GraphQL.Core.Generators.Sorting;

public class SortingGenerator : IMethodExtenderGenerator {
    private readonly ModuleBuilder _moduleBuilder;
    private readonly ScalarRegistry _scalarRegistry;

    public SortingGenerator(ModuleBuilder moduleBuilder, ScalarRegistry scalarRegistry) {
        _moduleBuilder = moduleBuilder;
        _scalarRegistry = scalarRegistry;
    }

    public MethodExtenderDescriptor GetMethodExtenderDescriptor(MemberInfo memberInfo, Type originalType, Type wrappedReturnType, ref int parameterCount) {
        if(!memberInfo.HasCustomAttribute<GraphQLSortAttribute>())
            return new MethodExtenderDescriptor();

        var sortParameterIndex = ++parameterCount;
        var astNodeParameterIndex = ++parameterCount;
        var contextParameterIndex = ++parameterCount;

        var isQueryable = wrappedReturnType.IsQueryable();
        // If isQueryable == true the generator will execute before wrapping. Hence we will use the unwrapped element type
        var elementType = originalType;

        var generatedSortType = GenerateSortType(elementType, isQueryable);
        return
            new MethodExtenderDescriptor(new[] { generatedSortType, typeof(GraphQLField), typeof(RequestContext) },
                methodBuilder => {
                    var parameterBuilder = methodBuilder.DefineParameter(sortParameterIndex, ParameterAttributes.HasDefault | ParameterAttributes.Optional, "sort");
                    parameterBuilder.SetConstant(null);
                    parameterBuilder.SetCustomAttribute(new CustomAttributeBuilder(typeof(GraphQLNullable).GetConstructors().First(), Array.Empty<object>()));

                    methodBuilder.DefineAnonymousResolvedParameter(astNodeParameterIndex);
                    methodBuilder.DefineAnonymousResolvedParameter(contextParameterIndex);
                },
                (step, ilGenerator) => {
                    if(step != MethodExtenderStep.BeforeWrap)
                        return;

                    if(isQueryable) {
                        var helperMethodInfo = typeof(SortingHelper).GetMethod(wrappedReturnType.IsTask() ? nameof(SortingHelper.SortQueryableTaskResult) : nameof(SortingHelper.SortQueryableResult), BindingFlags.Public | BindingFlags.Static)!;
                        helperMethodInfo = helperMethodInfo.MakeGenericMethod(originalType, generatedSortType);
                        GenerateEnumerableSort(helperMethodInfo, ilGenerator, sortParameterIndex, astNodeParameterIndex, contextParameterIndex);
                    } else if(!isQueryable) {
                        var helperMethodInfo = typeof(SortingHelper).GetMethod(wrappedReturnType.IsTask() ? nameof(SortingHelper.SortEnumerableTaskResult) : nameof(SortingHelper.SortEnumerableResult), BindingFlags.Public | BindingFlags.Static)!;
                        helperMethodInfo = helperMethodInfo.MakeGenericMethod(elementType, generatedSortType);
                        GenerateEnumerableSort(helperMethodInfo, ilGenerator, sortParameterIndex, astNodeParameterIndex, contextParameterIndex);
                    }
                });
    }

    private static void GenerateEnumerableSort(MethodInfo helperMethodInfo, ILGenerator ilGenerator, int sortParameterIndex, int astNodeParameterIndex, int contextParameterIndex) {
        ilGenerator.Emit(OpCodes.Ldarg_S, sortParameterIndex);
        ilGenerator.Emit(OpCodes.Ldarg_S, astNodeParameterIndex);
        ilGenerator.Emit(OpCodes.Ldarg_S, contextParameterIndex);
        ilGenerator.EmitCall(OpCodes.Call, helperMethodInfo, null);
    }

    private Type GenerateSortType(Type forType, bool isQueryable) {
        var sortType = _moduleBuilder.DefineType(forType.Name + "Sort");

        foreach(var member in forType.GetAllGraphQLMemberInfo()) {
            var subType = member switch {
                PropertyInfo propertyInfo => propertyInfo.PropertyType,
                MethodInfo methodInfo => methodInfo.ReturnType,
                _ => throw new ArgumentOutOfRangeException()
            };

            // Only properties is supported for IQueryable
            if(isQueryable && member is not PropertyInfo) {
                continue;
            }

            subType.IsCollection(out subType);
            if(!_scalarRegistry.IsValidGraphQLObjectType(subType)) {
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