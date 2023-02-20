﻿using Fireflies.GraphQL.Abstractions;

namespace Fireflies.GraphQL.FederationDemo;

public class RealAuthor : IAuthor {
    [GraphQlId]
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    [GraphQLDeprecated("Is not populated anymore")]
    public IEnumerable<string> Emails { get; set; } = Enumerable.Empty<string>();
}