using System.ComponentModel;

namespace Fireflies.Logging.Abstractions;

public interface IFirefliesLogger {
    void Error(Exception exception, [Localizable(false)] string message);
    void Debug([Localizable(false)] string message);
}