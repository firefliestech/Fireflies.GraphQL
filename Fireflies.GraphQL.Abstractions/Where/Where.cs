using Fireflies.GraphQL.Abstractions.Generator;

namespace Fireflies.GraphQL.Abstractions.Where;

public abstract class Where<T> {
    [GraphQLNullable]
    public T? Equal { get; set; }
    
    [GraphQLNullable]
    public T? NotEqual { get; set; }
}