using System.Reflection.Emit;
using System.Reflection;

namespace Fireflies.GraphQL.Core.Extensions;

internal static class EmitExtensions {
    public static CustomAttributeBuilder ToAttributeBuilder(this CustomAttributeData data) {
        if(data == null) {
            throw new ArgumentNullException(nameof(data));
        }

        var propertyArguments = new List<PropertyInfo>();
        var propertyArgumentValues = new List<object>();
        var fieldArguments = new List<FieldInfo>();
        var fieldArgumentValues = new List<object>();
        foreach(var namedArg in data.NamedArguments) {
            var fi = namedArg.MemberInfo as FieldInfo;
            var pi = namedArg.MemberInfo as PropertyInfo;

            if(fi != null) {
                fieldArguments.Add(fi);
                fieldArgumentValues.Add(namedArg.TypedValue.Value!);
            } else if(pi != null) {
                propertyArguments.Add(pi);
                propertyArgumentValues.Add(namedArg.TypedValue.Value!);
            }
        }

        return new CustomAttributeBuilder(data.Constructor,
            data.ConstructorArguments.Select(ctorArg => ctorArg.Value!).ToArray(),
            propertyArguments.ToArray(),
            propertyArgumentValues.ToArray(),
            fieldArguments.ToArray(),
            fieldArgumentValues.ToArray());
    }

    public static void DefineParameters(this MethodBuilder builder, ParameterInfo[] baseParameters) {
        InternalDefineParameters(baseParameters, (parameter, index) => builder.DefineParameter(index, parameter.HasDefaultValue ? ParameterAttributes.HasDefault : ParameterAttributes.None, parameter.Name));
    }

    public static void DefineParameters(this ConstructorBuilder builder, ParameterInfo[] baseParameters) {
        InternalDefineParameters(baseParameters, (parameter, index) => builder.DefineParameter(index, parameter.HasDefaultValue ? ParameterAttributes.HasDefault : ParameterAttributes.None, parameter.Name));
    }

    private static void InternalDefineParameters(ParameterInfo[] baseParameters, Func<ParameterInfo, int, ParameterBuilder> callback) {
        var i = 1;
        foreach(var parameter in baseParameters) {
            var parameterBuilder = callback(parameter, i);
            i++;

            foreach(var customAttributeData in parameter.GetCustomAttributesData()) {
                parameterBuilder.SetCustomAttribute(customAttributeData.ToAttributeBuilder());
            }

            if(parameter.HasDefaultValue)
                parameterBuilder.SetConstant(parameter.DefaultValue);
            else if(NullabilityChecker.IsNullable(parameter))
                parameterBuilder.SetConstant(null);
        }
    }
}