using System.Reflection;

namespace Fireflies.GraphQL.Core;

internal class OperationDescriptor {
    public Type Type { get; }
    public string Name { get; }
    public MethodInfo Method { get; }

    internal OperationDescriptor(string name, Type type, MethodInfo method) {
        Name = name;
        Type = type;
        Method = method;
    }
}