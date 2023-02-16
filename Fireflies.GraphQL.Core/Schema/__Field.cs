using Fireflies.GraphQL.Contract;
using Fireflies.GraphQL.Core.Extensions;
using System.Reflection;

namespace Fireflies.GraphQL.Core.Schema;

// ReSharper disable once InconsistentNaming
public class __Field {
    public string Name { get; set; }
    public string? Description { get; set; }
    public __InputValue[] Args { get; set; } = Array.Empty<__InputValue>();
    public __Type Type { get; set; }
    public bool IsDeprecated { get; set; }
    public string? DeprecationReason { get; set; }

    public __Field() {
    }

    public __Field(MemberInfo memberInfo) {
        Name = memberInfo.GraphQLName();
        Description = GetFieldDescription(memberInfo);

        var deprecationAttribute = memberInfo.GetCustomAttribute<GraphQLDeprecatedAttribute>();
        IsDeprecated = deprecationAttribute != null;
        DeprecationReason = deprecationAttribute?.Reason;
    }

    private string? GetFieldDescription(MemberInfo memberInfo) {
        string description = null;

        var descriptionAttribute = memberInfo.GetCustomAttribute<GraphQLDescriptionAttribute>();
        if(descriptionAttribute != null)
            description += descriptionAttribute.Description;

        var authorizationAttributes = memberInfo.GetCustomAttributes<GraphQLAuthorizationAttribute>();
        if(authorizationAttributes.Any()) {
            if(description != null) {
                description += "\n\n";
            }

            description += "Authorization required:\n" + string.Join("\nOR\n", authorizationAttributes.Select(x => x.Help));
        }

        return description;
    }
}