using System.Reflection.Emit;

namespace Fireflies.GraphQL.Core.Middleware;

public class DecoratorDescriptor {
    public Type[] ParameterTypes { get; set; }
    public Action<MethodBuilder> DefineParametersCallback { get; set; }
    public Action<ILGenerator> DecorateCallback { get; set; }
    public bool ShouldDecorate { get; set; }
}