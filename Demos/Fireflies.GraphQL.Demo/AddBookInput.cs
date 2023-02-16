using Fireflies.GraphQL.Abstractions;

namespace Fireflies.GraphQL.Demo;

public class AddBookInput : GraphQLInput {
    public string Title { get; set; }
}