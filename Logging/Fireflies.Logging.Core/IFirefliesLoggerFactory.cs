namespace Fireflies.Logging.Core;

public interface IFirefliesLoggerFactory {
    public IFirefliesLogger GetLogger<T>();
}