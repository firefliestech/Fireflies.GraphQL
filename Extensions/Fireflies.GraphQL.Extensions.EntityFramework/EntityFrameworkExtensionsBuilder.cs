﻿using Fireflies.GraphQL.Core;
using Fireflies.GraphQL.Core.Generators.Sorting;
using Fireflies.IoC.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Fireflies.GraphQL.Extensions.EntityFramework;

public class EntityFrameworkExtensionsBuilder : IExtensionBuilder {
    private readonly GraphQLOptionsBuilder _optionsBuilder;
    private readonly List<Type> _dbContexts = new();

    public EntityFrameworkExtensionsBuilder(GraphQLOptionsBuilder optionsBuilder) {
        _optionsBuilder = optionsBuilder;
    }

    public void Register<T>() where T : DbContext {
        _dbContexts.Add(typeof(T));
    }

    public void Build() {
        _optionsBuilder.AddGeneratorBefore<SortingGenerator>(new EntityFrameworkMethodExtender());
    }

    public void BuildRequestLifetimeScope(ILifetimeScopeBuilder lifetimeScopeBuilder) {
        foreach(var dbContext in _dbContexts)
            lifetimeScopeBuilder.RegisterType(dbContext);
    }

    public void BuildGraphQLLifetimeScope(ILifetimeScopeBuilder lifetimeScopeBuilder) {
    }
}