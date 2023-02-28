using System.Reflection;

namespace Fireflies.GraphQL.Core.Generators;

public interface IMethodExtenderGenerator : IGenerator {
    MethodExtenderDescriptor GetMethodExtenderDescriptor(MemberInfo memberInfo, Type wrappedReturnType, ref int parameterCount);
}