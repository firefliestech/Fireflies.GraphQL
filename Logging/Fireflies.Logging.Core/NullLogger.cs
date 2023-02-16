namespace Fireflies.Logging.Core;

public class NullLogger : IFirefliesLogger {
    public void Error(Exception exception, string message) {
    }

    public void Debug(string message) {
    }
}