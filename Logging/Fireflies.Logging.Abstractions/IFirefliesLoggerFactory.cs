namespace Fireflies.Logging.Abstractions;

public interface IFirefliesLoggerFactory {
    public IFirefliesLogger GetLogger<T>();
}