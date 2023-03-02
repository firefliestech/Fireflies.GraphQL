using System.Reflection.Emit;

namespace Fireflies.GraphQL.Core.Generators;

public class MethodExtenderDescriptor {
    public bool ShouldDecorate { get; }

    public Type[] ParameterTypes { get; }
    public Action<MethodBuilder> DefineParametersCallback { get; }
    
    public Action<MethodExtenderStep, ILGenerator> GenerateCallback { get; }

    public MethodExtenderDescriptor() {
        ShouldDecorate = false;
        ParameterTypes = Array.Empty<Type>();
        DefineParametersCallback = _ => { };
        GenerateCallback = (_, _) => { };
    }

    public MethodExtenderDescriptor(Type[] parameterTypes, Action<MethodBuilder> defineParametersCallback, Action<MethodExtenderStep, ILGenerator> generateCallback) {
        ShouldDecorate = true;
        ParameterTypes = parameterTypes;
        DefineParametersCallback = defineParametersCallback;
        GenerateCallback = generateCallback;
    }
}