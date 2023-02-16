using Fireflies.Logging.Abstractions;

namespace Fireflies.GraphQL.Core;

public class NullLoggerFactory : IFirefliesLoggerFactory {
    public IFirefliesLogger GetLogger<T>() {
        return new NullLogger();
    }
}