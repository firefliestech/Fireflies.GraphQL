using Fireflies.GraphQL.Contract;

namespace Fireflies.GraphQL.Demo;

public class AddBookInput : GraphQLInput {
    public string Title { get; set; }
}