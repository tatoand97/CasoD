namespace CasoD.Services;

internal sealed class ConsoleLog
{
    private readonly object _lock = new();

    public void Info(string stage, string message) => Write(Console.Out, "INFO", stage, message);

    public void Warn(string stage, string message) => Write(Console.Out, "WARN", stage, message);

    public void Error(string stage, string message) => Write(Console.Error, "ERROR", stage, message);

    private void Write(TextWriter writer, string level, string stage, string message)
    {
        lock (_lock)
        {
            writer.WriteLine($"[{level}] [{stage}] {message}");
        }
    }
}
