using System.Reflection;

namespace Fireflies.GraphQL.Core.Generators;

public interface IMethodExtenderGenerator : IGenerator {
    MethodExtenderDescriptor GetMethodExtenderDescriptor(MemberInfo memberInfo, ref int parameterCount);
}