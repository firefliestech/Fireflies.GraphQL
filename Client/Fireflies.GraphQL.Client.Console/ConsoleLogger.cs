public static class ConsoleLogger {
    public static void WriteInfo(string message) {
        Console.WriteLine(message);
    }

    public static void WriteWarning(string message) {
        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(message);
        Console.ForegroundColor = originalColor;
    }

    public static void WriteSuccess(string message) {
        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(message);
        Console.ForegroundColor = originalColor;
    }

    public static void WriteError(Exception exception, string message) {
        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"{message}\r\n{exception}");
        Console.ForegroundColor = originalColor;
    }

    public static void WriteError(string message) {
        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(message);
        Console.ForegroundColor = originalColor;
    }
}