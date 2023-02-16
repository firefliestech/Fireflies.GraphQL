namespace Fireflies.Logging.Core;

public class NullLoggerFactory : IFirefliesLoggerFactory {
    public IFirefliesLogger GetLogger<T>() {
        return new NullLogger();
    }
}