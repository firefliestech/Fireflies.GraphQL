namespace Fireflies.GraphQL.Core.Extensions;

public record struct AsyncParallelData<T>(T Value, int Index);