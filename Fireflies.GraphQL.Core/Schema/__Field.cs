using Fireflies.GraphQL.Core.Extensions;
using System.Reflection;
using Fireflies.GraphQL.Abstractions;
using Fireflies.GraphQL.Abstractions.Sorting;

namespace Fireflies.GraphQL.Core.Schema;

// ReSharper disable once InconsistentNaming
[GraphQLNoWrapper]
public class __Field {
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public __InputValue[] Args { get; set; } = Array.Empty<__InputValue>();
    public __Type Type { get; set; } = null!;
    public bool IsDeprecated { get; set; }
    public string? DeprecationReason { get; set; }

    // ReSharper disable once UnusedMember.Global
    // Used when Json.NET deserialize schema for federated queries
    public __Field() {
    }

    public __Field(MemberInfo memberInfo) {
        Name = memberInfo.GraphQLName();
        Description = GetFieldDescription(memberInfo);
        var deprecatedReason = memberInfo.GetDeprecatedReason();
        IsDeprecated = deprecatedReason != null;
        DeprecationReason = deprecatedReason;
    }

    private string? GetFieldDescription(MemberInfo memberInfo) {
        string? description = null;

        var descriptionAttribute = memberInfo.GetDescription();
        if(descriptionAttribute != null)
            description += descriptionAttribute;

        var authorizationAttributes = memberInfo.DeclaringType!.GetCustomAttributes<GraphQLAuthorizationAttribute>().Union(memberInfo.GetCustomAttributes<GraphQLAuthorizationAttribute>());
        if(authorizationAttributes.Any()) {
            if(description != null) {
                description += "\n\n";
            }

            description += "Authorization required:\n" + string.Join("\nOR\n", authorizationAttributes.Select(x => x.Help));
        }

        return description;
    }
}