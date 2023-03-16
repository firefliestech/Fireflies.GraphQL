﻿using Fireflies.GraphQL.Core.Extensions;
using System.Reflection;
using Fireflies.GraphQL.Abstractions.Where;
using System.Reflection.Emit;
using Fireflies.GraphQL.Abstractions.Generator;
using GraphQLParser.AST;

namespace Fireflies.GraphQL.Core.Generators.Where;

public class WhereGenerator : IMethodExtenderGenerator {
    private readonly ModuleBuilder _moduleBuilder;

    public WhereGenerator(ModuleBuilder moduleBuilder) {
        _moduleBuilder = moduleBuilder;
    }

    public MethodExtenderDescriptor GetMethodExtenderDescriptor(MemberInfo memberInfo, Type originalType, Type wrappedReturnType, ref int parameterCount) {
        if(!memberInfo.HasCustomAttribute<GraphQLWhereAttribute>())
            return new MethodExtenderDescriptor();

        var whereParameterIndex = ++parameterCount;
        var astNodeParameterIndex = ++parameterCount;
        var contextParameterIndex = ++parameterCount;
        var valueAccessorParameterIndex = ++parameterCount;

        var isQueryable = wrappedReturnType.IsQueryable();
        var elementType = originalType;

        var generatedWhereType = GenerateWhereType(elementType);

        return
            new MethodExtenderDescriptor(new[] { generatedWhereType, typeof(GraphQLField), typeof(IGraphQLContext), typeof(ValueAccessor) },
                methodBuilder => {
                    var parameterBuilder = methodBuilder.DefineParameter(whereParameterIndex, ParameterAttributes.HasDefault | ParameterAttributes.Optional, "where");
                    parameterBuilder.SetConstant(null);
                    parameterBuilder.SetCustomAttribute(new CustomAttributeBuilder(typeof(GraphQLNullable).GetConstructors().First(), Array.Empty<object>()));

                    methodBuilder.DefineAnonymousResolvedParameter(astNodeParameterIndex);
                    methodBuilder.DefineAnonymousResolvedParameter(contextParameterIndex);
                    methodBuilder.DefineAnonymousResolvedParameter(valueAccessorParameterIndex);
                },
                (step, ilGenerator) => {
                    if(step != MethodExtenderStep.BeforeWrap)
                        return;

                    if(isQueryable) {
                        var helperMethodInfo = typeof(WhereHelper).GetMethod(wrappedReturnType.IsTask() ? nameof(WhereHelper.WhereQueryableTaskResult) : nameof(WhereHelper.WhereQueryableResult), BindingFlags.Public | BindingFlags.Static)!;
                        helperMethodInfo = helperMethodInfo.MakeGenericMethod(originalType);
                        AddHelperCall(helperMethodInfo, ilGenerator, astNodeParameterIndex, contextParameterIndex, valueAccessorParameterIndex);
                    } else {
                        var helperMethodInfo = typeof(WhereHelper).GetMethod(wrappedReturnType.IsTask() ? nameof(WhereHelper.WhereEnumerableTaskResult) : nameof(WhereHelper.WhereEnumerableResult), BindingFlags.Public | BindingFlags.Static)!;
                        helperMethodInfo = helperMethodInfo.MakeGenericMethod(originalType);
                        AddHelperCall(helperMethodInfo, ilGenerator, astNodeParameterIndex, contextParameterIndex, valueAccessorParameterIndex);
                    }
                });
    }

    private static void AddHelperCall(MethodInfo helperMethodInfo, ILGenerator ilGenerator, int astNodeParameterIndex, int contextParameterIndex, int variableAccessorParameterIndex) {
        ilGenerator.Emit(OpCodes.Ldarg_S, astNodeParameterIndex);
        ilGenerator.Emit(OpCodes.Ldarg_S, contextParameterIndex);
        ilGenerator.Emit(OpCodes.Ldarg_S, variableAccessorParameterIndex);
        ilGenerator.EmitCall(OpCodes.Call, helperMethodInfo, null);
    }

    private Type GenerateWhereType(Type forType) {
        var whereType = _moduleBuilder.DefineType(forType.Name + "Where");

        foreach(var member in forType.GetAllGraphQLMemberInfo()) {
            var subType = member switch {
                PropertyInfo propertyInfo => propertyInfo.PropertyType,
                MethodInfo methodInfo => methodInfo.ReturnType,
                _ => throw new ArgumentOutOfRangeException()
            };

            if(member is not PropertyInfo) {
                continue;
            }

            subType.IsCollection(out subType);
            if(!subType.IsValidGraphQLObject()) {
                var graphQLType = subType.GetGraphQLBaseType();

                Type propertyType;
                if(graphQLType == typeof(string))
                    propertyType = typeof(StringWhere);
                else if(graphQLType == typeof(int))
                    propertyType = typeof(IntWhere);
                else if(graphQLType == typeof(decimal))
                    propertyType = typeof(DecimalWhere);
                else if(graphQLType == typeof(DateTime) || graphQLType == typeof(DateTimeOffset))
                    propertyType = typeof(DateTimeWhere);
                else if(graphQLType.IsClass)
                    propertyType = GenerateWhereType(graphQLType);
                else
                    throw new ArgumentOutOfRangeException("subType.GetGraphQLType()");

                DefineWhereProperty(propertyType, whereType, member);
            }
        }

        return whereType.CreateType()!;
    }

    private static void DefineWhereProperty(Type propertyType, TypeBuilder whereType, MemberInfo member) {
        var field = whereType.DefineField("_" + member.Name.LowerCaseFirstLetter(), propertyType, FieldAttributes.Private);

        var property = whereType.DefineProperty(member.Name, PropertyAttributes.HasDefault, CallingConventions.Standard, propertyType, Type.EmptyTypes);
        property.SetCustomAttribute(new CustomAttributeBuilder(typeof(GraphQLNullable).GetConstructors().First(), Array.Empty<object>()));

        var getMethod = whereType.DefineMethod($"get_{member.Name}", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.Virtual, CallingConventions.Standard, propertyType, Type.EmptyTypes);

        property.SetGetMethod(getMethod);
        var getILGenerator = getMethod.GetILGenerator();
        getILGenerator.Emit(OpCodes.Ldarg_0);
        getILGenerator.Emit(OpCodes.Ldfld, field);
        getILGenerator.Emit(OpCodes.Ret);

        var setMethod = whereType.DefineMethod($"set_{member.Name}", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.Virtual, CallingConventions.Standard, typeof(void), new[] { propertyType });

        var setILGenerator = setMethod.GetILGenerator();
        setILGenerator.Emit(OpCodes.Ldarg_0);
        setILGenerator.Emit(OpCodes.Ldarg_1);
        setILGenerator.Emit(OpCodes.Stfld, field);
        setILGenerator.Emit(OpCodes.Ret);

        property.SetSetMethod(setMethod);
    }
}