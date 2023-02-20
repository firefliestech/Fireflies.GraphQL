using Fireflies.Logging.Abstractions;

namespace Fireflies.GraphQL.Core;

public class NullLogger : IFirefliesLogger {
    public void Error(Exception exception, string message) {
    }

    public void Debug(string message) {
    }

    public void Trace(string message) {
    }

    public void Info(string message) {
    }
}