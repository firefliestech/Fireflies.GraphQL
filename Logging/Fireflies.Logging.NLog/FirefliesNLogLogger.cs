using Fireflies.Logging.Core;
using NLog;

namespace Fireflies.Logging.NLog;

public class FirefliesNLogLogger : IFirefliesLogger {
    private readonly Logger _logger;

    public FirefliesNLogLogger(Logger logger) {
        _logger = logger;
    }

    public void Error(Exception exception, string message) {
        _logger.Error(exception, message);
    }

    public void Debug(string message) {
        _logger.Debug(message);
    }
}