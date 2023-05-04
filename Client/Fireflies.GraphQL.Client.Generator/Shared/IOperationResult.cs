public interface IOperationResult<T> {
    IEnumerable<IClientError> Errors { get; }
    T Data { get; }
}