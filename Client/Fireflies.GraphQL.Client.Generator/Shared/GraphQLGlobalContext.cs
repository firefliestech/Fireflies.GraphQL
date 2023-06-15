public class GraphQLGlobalContext : IGraphQLGlobalContext {
    public event Action<IGraphQLClient>? RequestStarted;
    public event Action<IGraphQLClient>? RequestEnded;

    public void TriggerRequestStarted(IGraphQLClient client) {
        RequestStarted?.Invoke(client);
    }

    public void TriggerRequestEnded(IGraphQLClient client) {
        RequestEnded?.Invoke(client);
    }
}
