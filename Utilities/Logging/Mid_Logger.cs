// Utilities/Logging/Mid_Logger.cs
using System.Diagnostics;
using Microsoft.JSInterop;

namespace SubashaVentures.Utilities.Logging;

/// <summary>
/// Debug message type enumeration
/// </summary>
public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Exception,
    Error,
    Critical
}

/// <summary>
/// Centralized logging service for use across projects
/// Supports ILogger, browser console, and System.Diagnostics.Debug
/// Thread-safe and dependency injection ready
/// </summary>
public interface IMid_Logger
{
    void Initialize(ILogger? logger = null, IJSRuntime? jsRuntime = null);
    void Log(string message, LogLevel level = LogLevel.Debug);
    Task LogAsync(string message, LogLevel level = LogLevel.Debug);

    void LogException(Exception ex, string context = "");
     Task LogExceptionAsync(Exception ex, string context = "");
    void SetDebugMode(bool enabled);
    bool IsDebugEnabled { get; }
}

/// <summary>
/// Implementation of Mid_Logger
/// </summary>
public class Mid_Logger : IMid_Logger
{
    private volatile bool _isDebugMode = true;
    private ILogger? _logger;
    private IJSRuntime? _jsRuntime;
    private readonly object _lockObject = new object();

    public bool IsDebugEnabled => _isDebugMode;

    /// <summary>
    /// Initialize the logger with optional dependencies
    /// </summary>
    /// <param name="logger">Optional ILogger instance for server-side logging</param>
    /// <param name="jsRuntime">Optional IJSRuntime for browser console logging</param>
    public void Initialize(ILogger? logger = null, IJSRuntime? jsRuntime = null)
    {
        lock (_lockObject)
        {
            _logger = logger;
            _jsRuntime = jsRuntime;
        }
    }

    /// <summary>
    /// Set whether debug logging is enabled
    /// </summary>
    public void SetDebugMode(bool enabled)
    {
        lock (_lockObject)
        {
            _isDebugMode = enabled;
            LogAsync($"Debug mode set to: {enabled}", LogLevel.Info).GetAwaiter().GetResult();
        }
    }

    /// <summary>
    /// Log a message synchronously (fire-and-forget)
    /// </summary>
    public void Log(string message, LogLevel level = LogLevel.Debug)
    {
        if (!_isDebugMode || string.IsNullOrEmpty(message))
            return;

        _ = Task.Run(async () => await LogAsync(message, level));
    }

    /// <summary>
    /// Log a message asynchronously
    /// </summary>
    public async Task LogAsync(string message, LogLevel level = LogLevel.Debug)
    {
        if (!_isDebugMode || string.IsNullOrEmpty(message))
            return;

        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var formattedMessage = $"[{timestamp}] [{level}] {message}";

        await LogToILogger(formattedMessage, level);
        await LogToJSConsole(formattedMessage, level);
        Debug.WriteLine(formattedMessage);
    }

    /// <summary>
    /// Log an exception with context
    /// </summary>
    public void LogException(Exception ex, string context = "")
    {
        var message = string.IsNullOrEmpty(context)
            ? $"Exception: {ex.Message}\n{ex.StackTrace}"
            : $"Exception in {context}: {ex.Message}\n{ex.StackTrace}";

        Log(message, LogLevel.Error);
    }

    /// <summary>
    /// Log an exception asynchronously
    /// </summary>
    public async Task LogExceptionAsync(Exception ex, string context = "")
    {
        var message = string.IsNullOrEmpty(context)
            ? $"Exception: {ex.Message}\n{ex.StackTrace}"
            : $"Exception in {context}: {ex.Message}\n{ex.StackTrace}";

        await LogAsync(message, LogLevel.Error);
    }

    private async Task LogToILogger(string message, LogLevel level)
    {
        if (_logger == null)
            return;

        await Task.Run(() =>
        {
            switch (level)
            {
                case LogLevel.Debug:
                    _logger.LogDebug("{Message}", message);
                    break;
                case LogLevel.Info:
                    _logger.LogInformation("{Message}", message);
                    break;
                case LogLevel.Warning:
                    _logger.LogWarning("{Message}", message);
                    break;
                case LogLevel.Error:
                    _logger.LogError("{Message}", message);
                    break;
                case LogLevel.Critical:
                    _logger.LogCritical("{Message}", message);
                    break;
            }
        });
    }

    private async Task LogToJSConsole(string message, LogLevel level)
    {
        if (_jsRuntime == null)
            return;

        try
        {
            var consoleMethod = level switch
            {
                LogLevel.Debug => "debug",
                LogLevel.Info => "info",
                LogLevel.Warning => "warn",
                LogLevel.Error => "error",
                LogLevel.Critical => "error",
                _ => "log"
            };

            await _jsRuntime.InvokeVoidAsync($"console.{consoleMethod}", message);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"JSInterop logging failed: {ex.Message}");
        }
    }
}