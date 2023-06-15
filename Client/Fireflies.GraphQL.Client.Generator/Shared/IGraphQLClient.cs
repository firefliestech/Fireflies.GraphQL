public interface IGraphQLClient {
    event Action? RequestStarted;
    event Action? RequestEnded;
}
