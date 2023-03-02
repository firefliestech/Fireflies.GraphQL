using System.Reflection;
using System.Reflection.Emit;
using Fireflies.GraphQL.Core;
using Fireflies.GraphQL.Core.Extensions;
using Fireflies.GraphQL.Core.Generators;
using GraphQLParser.AST;
using GraphQLParser.Visitors;
using Microsoft.EntityFrameworkCore;

namespace Fireflies.GraphQL.Extensions.EntityFramework;

public class EntityFrameworkMethodExtender : IMethodExtenderGenerator {
    public MethodExtenderDescriptor GetMethodExtenderDescriptor(MemberInfo memberInfo, Type originalType, Type wrappedReturnType, ref int parameterCount) {
        if(!wrappedReturnType.IsQueryable())
            return new MethodExtenderDescriptor();

        var astNodeParameterIndex = ++parameterCount;
        var graphQLOptionsParameterIndex = ++parameterCount;

        return new MethodExtenderDescriptor(new[] { typeof(GraphQLField), typeof(IGraphQLContext) },
            methodBuilder => {
                methodBuilder.DefineParameter(astNodeParameterIndex, ParameterAttributes.HasDefault | ParameterAttributes.Optional, Guid.NewGuid().ToString("N"))
                    .SetCustomAttribute(new CustomAttributeBuilder(typeof(ResolvedAttribute).GetConstructors().First(), Array.Empty<object>()));

                methodBuilder.DefineParameter(graphQLOptionsParameterIndex, ParameterAttributes.HasDefault | ParameterAttributes.Optional, Guid.NewGuid().ToString("N"))
                    .SetCustomAttribute(new CustomAttributeBuilder(typeof(ResolvedAttribute).GetConstructors().First(), Array.Empty<object>()));
            },
            (step, ilGenerator) => {
                if(step != MethodExtenderStep.BeforeWrap)
                    return;

                var helperMethodInfo = typeof(EntityFrameworkMethodExtender).GetMethod(wrappedReturnType.IsTask() ? nameof(ExtendTaskResult) : nameof(ExtendResult), BindingFlags.Public | BindingFlags.Static)!;
                helperMethodInfo = helperMethodInfo.MakeGenericMethod(originalType);
                ilGenerator.Emit(OpCodes.Ldarg_S, astNodeParameterIndex);
                ilGenerator.Emit(OpCodes.Ldarg_S, graphQLOptionsParameterIndex);
                ilGenerator.EmitCall(OpCodes.Call, helperMethodInfo, null);
            });
    }

    public static Task<IQueryable<TElement>?> ExtendTaskResult<TElement>(Task<IQueryable<TElement>?> resultTask, GraphQLField graphQLField, IGraphQLContext graphQLContext) {
        return resultTask.ContinueWith(taskResult => {
            if(taskResult.Result == null)
                return null;

            var provider = taskResult.Result.Provider;
            if(provider.GetType().Name != "EntityQueryProvider")
                return taskResult.Result;

            var includeVisitor = (IncludeVisitor)Activator.CreateInstance(typeof(IncludeVisitor<>).MakeGenericType(typeof(TElement)), taskResult.Result, graphQLContext)!;
            return (IQueryable<TElement>?)includeVisitor.Execute(graphQLField);
        });
    }

    public static IQueryable<TElement>? ExtendResult<TElement>(IQueryable<TElement>? result, GraphQLField graphQLField, IGraphQLContext graphQLContext) where TElement : class {
        return ExtendTaskResult(Task.FromResult(result), graphQLField, graphQLContext).Result;
    }

    private abstract class IncludeVisitor : ASTVisitor<IGraphQLContext> {
        public abstract object Execute(ASTNode startNode);
    }

    private class IncludeVisitor<TElement> : IncludeVisitor where TElement : class {
        private readonly IGraphQLContext _context;
        private readonly Stack<(string PropertyName, Type Type)> _path = new();

        private IQueryable<TElement> _result;
        private bool isFirst = true;

        public IncludeVisitor(IQueryable<TElement> queryable, IGraphQLContext context) {
            _context = context;
            _result = queryable;
        }

        public override object Execute(ASTNode startNode) {
            VisitAsync(startNode, _context).GetAwaiter().GetResult();
            return _result;
        }

        protected override async ValueTask VisitFieldAsync(GraphQLField field, IGraphQLContext context) {
            if(field.SelectionSet == null || field.SelectionSet.Selections.Count == 0)
                return;

            if(isFirst) {
                isFirst = false;
                await base.VisitFieldAsync(field, context);
            } else {
                if(!_path.TryPeek(out var parent)) {
                    parent = ("", typeof(TElement));
                }

                var property = parent.Type.GetProperty(field.Name.StringValue, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
                _path.Push((property!.Name, property.PropertyType));
                _result = _result.Include(string.Join(".", _path.Select(x => x.PropertyName).Reverse()));
                await base.VisitFieldAsync(field, context);

                _path.Pop();
            }
        }
    }
}