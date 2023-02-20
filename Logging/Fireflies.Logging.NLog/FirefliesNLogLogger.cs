using Fireflies.Logging.Abstractions;
using NLog;

namespace Fireflies.Logging.NLog;

public class FirefliesNLogLogger : IFirefliesLogger {
    private readonly Logger _logger;

    public FirefliesNLogLogger(Logger logger) {
        _logger = logger;
    }

    public void Trace(string message) {
        _logger.Trace(message);
    }

    public void Debug(string message) {
        _logger.Debug(message);
    }

    public void Info(string message) {
        _logger.Info(message);
    }

    public void Error(Exception exception, string message) {
        _logger.Error(exception, message);
    }
}