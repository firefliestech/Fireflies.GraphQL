namespace Fireflies.GraphQL.Client.Generator.Builders;

public interface ITypeBuilder {
    public Task Build();
    public string Source();
}