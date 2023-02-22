using System.Reflection;

namespace Fireflies.GraphQL.Core.Middleware;

public interface IDecoratorMiddleware : IMiddleware {
    DecoratorDescriptor GetDecoratorDescription(MemberInfo memberInfo, ref int parameterCount);
}