public interface IGraphQLGlobalContext {
    event Action<IGraphQLClient>? RequestStarted;
    event Action<IGraphQLClient>? RequestEnded;

    void TriggerRequestStarted(IGraphQLClient client);
    void TriggerRequestEnded(IGraphQLClient client);
}
