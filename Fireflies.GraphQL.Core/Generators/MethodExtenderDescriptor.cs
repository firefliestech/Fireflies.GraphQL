using System.Reflection.Emit;

namespace Fireflies.GraphQL.Core.Generators;

public class MethodExtenderDescriptor {
    public Type[] ParameterTypes { get; }
    public Action<MethodBuilder> DefineParametersCallback { get; }
    public Action<ILGenerator> DecorateCallback { get; }
    public bool ShouldDecorate { get; }

    public MethodExtenderDescriptor() {
        ShouldDecorate = false;
        ParameterTypes = Array.Empty<Type>();
        DefineParametersCallback = _ => { };
        DecorateCallback = _ => { };
    }

    public MethodExtenderDescriptor(Type[] parameterTypes, Action<MethodBuilder> defineParametersCallback, Action<ILGenerator> decorateCallback) {
        ShouldDecorate = true;
        ParameterTypes = parameterTypes;
        DefineParametersCallback = defineParametersCallback;
        DecorateCallback = decorateCallback;
    }
}