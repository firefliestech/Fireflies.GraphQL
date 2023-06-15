public interface IOperationResult {
    IEnumerable<IClientError> Errors { get; }
}

public interface IOperationResult<T> : IOperationResult {
    T? Data { get; }
}