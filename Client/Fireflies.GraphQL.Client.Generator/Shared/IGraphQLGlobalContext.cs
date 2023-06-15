public interface IGraphQLGlobalContext {
    event Action<IGraphQLClient>? RequestStarted;
    event Action<IGraphQLClient, IOperationResult?>? RequestEnded;

    void TriggerRequestStarted(IGraphQLClient client);
    void TriggerRequestEnded(IGraphQLClient client, IOperationResult? result);
}
