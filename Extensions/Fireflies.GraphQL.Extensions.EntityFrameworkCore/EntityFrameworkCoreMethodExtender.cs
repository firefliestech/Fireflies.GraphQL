using System.Reflection;
using System.Reflection.Emit;
using Fireflies.GraphQL.Core;
using Fireflies.GraphQL.Core.Extensions;
using Fireflies.GraphQL.Core.Generators;
using Fireflies.GraphQL.Core.Generators.Connection;
using Fireflies.Utility.Reflection;
using GraphQLParser.AST;
using GraphQLParser.Visitors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.Internal;

namespace Fireflies.GraphQL.Extensions.EntityFrameworkCore;

public class EntityFrameworkCoreMethodExtender : IMethodExtenderGenerator {
    public MethodExtenderDescriptor GetMethodExtenderDescriptor(MemberInfo memberInfo, Type originalType, Type wrappedReturnType, ref int parameterCount) {
        if(!wrappedReturnType.IsQueryable())
            return new MethodExtenderDescriptor();

        var astNodeParameterIndex = ++parameterCount;
        var graphQLOptionsParameterIndex = ++parameterCount;
        var resultContext = ++parameterCount;

        return new MethodExtenderDescriptor(new[] { typeof(GraphQLField), typeof(IGraphQLContext), typeof(ResultContext) },
            methodBuilder => {
                methodBuilder.DefineAnonymousResolvedParameter(astNodeParameterIndex);
                methodBuilder.DefineAnonymousResolvedParameter(graphQLOptionsParameterIndex);
                methodBuilder.DefineAnonymousResolvedParameter(resultContext);
            },
            (step, ilGenerator) => {
                if(step != MethodExtenderStep.BeforeWrap)
                    return;

                var helperMethodInfo = typeof(EntityFrameworkCoreMethodExtender).GetMethod(wrappedReturnType.IsTask() ? nameof(ExtendTaskResult) : nameof(ExtendResult), BindingFlags.Public | BindingFlags.Static)!;
                helperMethodInfo = helperMethodInfo.MakeGenericMethod(originalType);
                ilGenerator.Emit(OpCodes.Ldarg_S, astNodeParameterIndex);
                ilGenerator.Emit(OpCodes.Ldarg_S, graphQLOptionsParameterIndex);
                ilGenerator.Emit(OpCodes.Ldarg_S, resultContext);
                ilGenerator.EmitCall(OpCodes.Call, helperMethodInfo, null);
            });
    }

    public static Task<IQueryable<TElement>?> ExtendTaskResult<TElement>(Task<IQueryable<TElement>?> resultTask, GraphQLField graphQLField, IGraphQLContext graphQLContext, ResultContext resultContext) {
        return resultTask.ContinueWith(taskResult => {
            if(taskResult.Result == null)
                return null;

            Console.WriteLine(resultContext);
            var provider = taskResult.Result.Provider;
            if(provider is not EntityQueryProvider)
                return taskResult.Result;

            var includeVisitor = (IncludeVisitor)Activator.CreateInstance(typeof(IncludeVisitor<>).MakeGenericType(typeof(TElement)), taskResult.Result, graphQLContext)!;
            if(resultContext.Any(x => x.IsAssignableTo(typeof(ConnectionBase)))) {
                var edgesField = graphQLField.SelectionSet?.Selections.OfType<GraphQLField>().FirstOrDefault(x => x.Name.StringValue == "edges");
                var nodeField = edgesField?.SelectionSet?.Selections.OfType<GraphQLField>().FirstOrDefault(x => x.Name.StringValue == "node");
                if(nodeField != null)
                    return (IQueryable<TElement>?)includeVisitor.Execute(nodeField);

                return taskResult.Result;
            }

            return (IQueryable<TElement>?)includeVisitor.Execute(graphQLField);
        });
    }

    public static IQueryable<TElement>? ExtendResult<TElement>(IQueryable<TElement>? result, GraphQLField graphQLField, IGraphQLContext graphQLContext, ResultContext resultContext) where TElement : class {
        return ExtendTaskResult(Task.FromResult(result), graphQLField, graphQLContext, resultContext).Result;
    }

    private abstract class IncludeVisitor : ASTVisitor<IGraphQLContext> {
        public abstract object Execute(ASTNode startNode);
    }

    private class IncludeVisitor<TElement> : IncludeVisitor where TElement : class {
        private readonly IGraphQLContext _context;
        private readonly Stack<(string PropertyName, Type Type)> _path = new();

        private IQueryable<TElement> _result;
        private bool _isFirst = true;

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

            if(_isFirst) {
                _isFirst = false;
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