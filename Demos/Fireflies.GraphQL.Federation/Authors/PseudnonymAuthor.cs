﻿namespace Fireflies.GraphQL.FederationDemo.Authors;

public class PseudnonymAuthor : IAuthor {
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public DateTimeOffset? Born { get; set; } = DateTimeOffset.MinValue;
}