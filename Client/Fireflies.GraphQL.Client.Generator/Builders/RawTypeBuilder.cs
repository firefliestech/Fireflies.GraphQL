using System.Text;

namespace Fireflies.GraphQL.Client.Generator.Builders;

public class RawTypeBuilder : ITypeBuilder {
    private readonly StringBuilder _stringBuilder;

    public RawTypeBuilder() {
        _stringBuilder = new StringBuilder();
    }

    public void Append(string value) {
        _stringBuilder.Append(value);
    }

    public void AppendLine() {
        _stringBuilder.AppendLine();
    }

    public void AppendLine(string value) {
        _stringBuilder.AppendLine(value);
    }

    public Task Build() {
        return Task.CompletedTask;
    }

    public string Source() {
        return _stringBuilder.ToString();
    }
}