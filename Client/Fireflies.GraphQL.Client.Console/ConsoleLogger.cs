namespace Fireflies.GraphQL.Client.Console; 

public static class ConsoleLogger {
    public static void WriteInfo(string message) {
        System.Console.WriteLine(message);
    }

    public static void WriteWarning(string message) {
        var originalColor = System.Console.ForegroundColor;
        System.Console.ForegroundColor = ConsoleColor.Yellow;
        System.Console.WriteLine(message);
        System.Console.ForegroundColor = originalColor;
    }

    public static void WriteSuccess(string message) {
        var originalColor = System.Console.ForegroundColor;
        System.Console.ForegroundColor = ConsoleColor.Green;
        System.Console.WriteLine(message);
        System.Console.ForegroundColor = originalColor;
    }

    public static void WriteError(Exception exception, string message) {
        var originalColor = System.Console.ForegroundColor;
        System.Console.ForegroundColor = ConsoleColor.Red;
        System.Console.WriteLine($"{message}\r\n{exception}");
        System.Console.ForegroundColor = originalColor;
    }

    public static void WriteError(string message) {
        var originalColor = System.Console.ForegroundColor;
        System.Console.ForegroundColor = ConsoleColor.Red;
        System.Console.WriteLine(message);
        System.Console.ForegroundColor = originalColor;
    }
}