namespace Stockflow.Webserver.Logging;

/// <summary>
/// Removes CR/LF from user-controlled strings before they reach the logger,
/// preventing forged/injected log entries (CodeQL "Log entries created from user input").
/// </summary>
internal static class LogSanitizer
{
    public static string Clean(string? value) =>
        value is null ? string.Empty : value.Replace("\r", "").Replace("\n", "");
}
