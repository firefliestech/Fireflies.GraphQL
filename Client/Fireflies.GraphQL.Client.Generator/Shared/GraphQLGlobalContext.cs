public class GraphQLGlobalContext : IGraphQLGlobalContext {
    public event Action<IGraphQLClient>? RequestStarted;
    public event Action<IGraphQLClient, IOperationResult?>? RequestEnded;

    public void TriggerRequestStarted(IGraphQLClient client) {
        RequestStarted?.Invoke(client);
    }

    public void TriggerRequestEnded(IGraphQLClient client, IOperationResult? result) {
        RequestEnded?.Invoke(client, result);
    }
}
