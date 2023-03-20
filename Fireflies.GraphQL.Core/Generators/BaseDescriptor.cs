using System.Reflection;
using System.Reflection.Emit;

namespace Fireflies.GraphQL.Core.Generators;

public class BaseDescriptor {
    public MemberInfo MemberInfo { get; set; }
    public IEnumerable<Type> ParameterTypes { get; set; }
    public Type ReturnType { get; set; }
    public bool GeneratingInterface { get; set; }
    public IEnumerable<Action<MethodBuilder>> DefineParameterCallbacks { get; set; }
}