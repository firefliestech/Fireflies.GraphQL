using Fireflies.Logging.Abstractions;
using NLog;

namespace Fireflies.Logging.NLog;

public class FirefliesNLogFactory : IFirefliesLoggerFactory {
    public IFirefliesLogger GetLogger<T>() {
        return new FirefliesNLogLogger(LogManager.GetLogger(typeof(T).Name));
    }
}