using System.Reflection;
using System.Reflection.Emit;

namespace Fireflies.GraphQL.Core.Generators;

public interface ITypeExtenderGenerator : IGenerator {
    void Extend(TypeBuilder typeBuilder, MethodBuilder wrappedMethod, MemberInfo baseMember, FieldBuilder instanceField, MethodExtenderDescriptor[] decoratorDescriptors);
}