// Utilities/HelperScripts/MID_HelperFunctions.cs
using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Serialization;
using SubashaVentures.Utilities.Logging;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;
namespace SubashaVentures.Utilities.HelperScripts;

/// <summary>
/// Helper functions for common operations with improved best practices
/// Logging has been extracted to Mid_Logger for reusability
/// </summary>
public static class MID_HelperFunctions
{
    #region Private Fields

    private static IMid_Logger? _logger;
    private static readonly object _lockObject = new object();

    // Cache for compiled regex patterns
    private static readonly ConcurrentDictionary<string, Regex> _regexCache = new();

    // Validation constants
    private static readonly string[] InvalidStringValues = { "NULL", "UNDEFINED", "NONE", "N/A" };

    // Debug utilities constants
    private const int MaxDepth = 10;
    private const int MaxCollectionItems = 100;

    private static readonly HashSet<Type> PrimitiveTypes = new()
    {
        typeof(string), typeof(decimal), typeof(DateTime), typeof(DateTimeOffset),
        typeof(TimeSpan), typeof(Guid)
    };

    #endregion

    #region Initialization

    /// <summary>
    /// Initialize the helper functions with a logger
    /// </summary>
    /// <param name="logger">IMid_Logger instance</param>
    public static void Initialize(IMid_Logger logger)
    {
        lock (_lockObject)
        {
            _logger = logger;
        }
    }

    #endregion

    #region String Validation

    /// <summary>
    /// Validates if a string is not null, empty, or variations of invalid values
    /// </summary>
    /// <param name="input">The string to validate</param>
    /// <returns>True if the string is valid, false otherwise</returns>
    public static bool IsValidString(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var upperInput = input.Trim().ToUpperInvariant();
        return !InvalidStringValues.Contains(upperInput);
    }

    /// <summary>
    /// Validates if a string matches a specific regex pattern with caching
    /// </summary>
    /// <param name="input">The string to validate</param>
    /// <param name="pattern">The regex pattern to match against</param>
    /// <returns>True if the string matches the pattern, false otherwise</returns>
    public static bool IsValidPattern(string? input, string pattern)
    {
        if (!IsValidString(input) || string.IsNullOrEmpty(pattern))
            return false;

        try
        {
            var regex = _regexCache.GetOrAdd(pattern, p => new Regex(p, RegexOptions.Compiled));
            return regex.IsMatch(input!);
        }
        catch (Exception ex)
        {
            _logger?.LogException(ex, "Regex validation");
            return false;
        }
    }

    #endregion

    #region Debug and Logging

    /// <summary>
    /// Print debug messages synchronously (fire-and-forget)
    /// </summary>
    /// <param name="message">The message to print</param>
    /// <param name="level">The log level</param>
    public static void DebugMessage(string message, LogLevel level = LogLevel.Debug)
    {
        _logger?.Log(message, level);
    }

    /// <summary>
    /// Print debug messages asynchronously
    /// </summary>
    /// <param name="message">The message to print</param>
    /// <param name="level">The log level</param>
    public static async Task DebugMessageAsync(string message, LogLevel level = LogLevel.Debug)
    {
        if (_logger == null)
            return;

        await _logger.LogAsync(message, level);
    }

    /// <summary>
    /// Log an exception
    /// </summary>
    public static void LogException(Exception ex, string context = "")
    {
        _logger?.LogException(ex, context);
    }

    /// <summary>
    /// Log an exception asynchronously
    /// </summary>
    public static async Task LogExceptionAsync(Exception ex, string context = "")
    {
        if (_logger == null)
            return;

        await _logger.LogExceptionAsync(ex, context);
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Get the current environment (Development, Production, etc.)
    /// </summary>
    /// <returns>The current environment name</returns>
    public static string GetEnvironment()
    {
#if DEBUG
        return "Development";
#else
        return "Production";
#endif
    }

    /// <summary>
    /// Executes a task with timeout and proper cancellation
    /// </summary>
    /// <typeparam name="T">The task return type</typeparam>
    /// <param name="task">The task to execute</param>
    /// <param name="timeoutMs">Timeout in milliseconds</param>
    /// <returns>The task result or default if timed out</returns>
    public static async Task<T?> ExecuteWithTimeout<T>(Task<T> task, int timeoutMs)
    {
        using var cts = new CancellationTokenSource(timeoutMs);

        try
        {
            return await task.WaitAsync(cts.Token);
        }
        catch (TimeoutException)
        {
            await DebugMessageAsync($"Task timed out after {timeoutMs} ms", LogLevel.Warning);
            return default;
        }
        catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
        {
            await DebugMessageAsync($"Task was cancelled due to timeout after {timeoutMs} ms", LogLevel.Warning);
            return default;
        }
    }

    /// <summary>
    /// Safe try-catch wrapper for actions with detailed error reporting
    /// </summary>
    /// <param name="action">The action to execute</param>
    /// <param name="actionName">Optional name for the action (for logging)</param>
    /// <returns>True if successful, false otherwise</returns>
    public static bool SafeExecute(Action action, string? actionName = null)
    {
        try
        {
            action();
            return true;
        }
        catch (Exception ex)
        {
            var errorMessage = actionName != null
                ? $"SafeExecute error in '{actionName}': {ex.Message}"
                : $"SafeExecute error: {ex.Message}";

            _logger?.LogException(ex, actionName);
            return false;
        }
    }

    /// <summary>
    /// Safe try-catch wrapper for async functions
    /// </summary>
    /// <typeparam name="T">Return type</typeparam>
    /// <param name="func">The function to execute</param>
    /// <param name="actionName">Optional name for the action (for logging)</param>
    /// <returns>The result or default value if exception occurs</returns>
    public static async Task<T?> SafeExecuteAsync<T>(Func<Task<T>> func, string? actionName = null)
    {
        try
        {
            return await func();
        }
        catch (Exception ex)
        {
            _logger?.LogException(ex, actionName);
            return default;
        }
    }

    /// <summary>
    /// Generate a cryptographically secure random string with specified length
    /// </summary>
    /// <param name="length">The length of the string</param>
    /// <param name="useSpecialCharacters">Include special characters</param>
    /// <returns>A random string</returns>
    public static string GenerateRandomString(int length, bool useSpecialCharacters = false)
    {
        if (length <= 0) return string.Empty;

        const string basicChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        const string specialChars = "!@#$%^&*()_+-=[]{}|;:,.<>?";

        var chars = useSpecialCharacters ? basicChars + specialChars : basicChars;

        var random = new Random();
        return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }

    #endregion

    #region Debug Utilities and Reflection

    /// <summary>
    /// Get detailed member values of a struct or class instance
    /// </summary>
    /// <typeparam name="T">The type of the object</typeparam>
    /// <param name="structOrClassInstance">The instance to inspect</param>
    /// <returns>A formatted string with all member values</returns>
    public static string GetStructOrClassMemberValues<T>(T? structOrClassInstance) where T : class
    {
        if (structOrClassInstance == null)
            return "null";

        var result = new StringBuilder();
        var visitedObjects = new HashSet<object>();

        GetStructOrClassMemberValuesRecursive(structOrClassInstance, result, 0, visitedObjects);
        return result.ToString();
    }

    private static void GetStructOrClassMemberValuesRecursive(object? structOrClassInstance, StringBuilder result, int depth, HashSet<object> visitedObjects)
    {
        if (structOrClassInstance == null)
        {
            result.AppendLine("null");
            return;
        }

        if (depth >= MaxDepth)
        {
            result.AppendLine($"{GetIndentation(depth)}[Max depth reached]");
            return;
        }

        if (visitedObjects.Contains(structOrClassInstance))
        {
            result.AppendLine($"{GetIndentation(depth)}[Circular reference detected]");
            return;
        }

        visitedObjects.Add(structOrClassInstance);

        try
        {
            var type = structOrClassInstance.GetType();
            var bindingFlags = type.IsClass
                ? BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly
                : BindingFlags.Public | BindingFlags.Instance;

            var fields = type.GetFields(bindingFlags);
            foreach (var field in fields)
            {
                AppendMemberValue(result, field.Name, field.GetValue(structOrClassInstance), depth, visitedObjects);
            }

            var properties = type.GetProperties(bindingFlags);
            foreach (var property in properties)
            {
                if (property.CanRead && property.GetIndexParameters().Length == 0)
                {
                    try
                    {
                        var value = property.GetValue(structOrClassInstance);
                        AppendMemberValue(result, property.Name, value, depth, visitedObjects);
                    }
                    catch (Exception ex)
                    {
                        result.AppendLine($"{GetIndentation(depth)}{GetArrowIndentation(depth)} {property.Name} :: [Error: {ex.Message}]");
                    }
                }
            }
        }
        finally
        {
            visitedObjects.Remove(structOrClassInstance);
        }
    }

    private static void AppendMemberValue(StringBuilder result, string name, object? value, int depth, HashSet<object> visitedObjects)
    {
        var indentation = GetIndentation(depth);
        var arrowIndentation = GetArrowIndentation(depth);

        if (value == null)
        {
            result.AppendLine($"{indentation}{arrowIndentation} {name} :: null");
        }
        else if (IsSimpleType(value.GetType()))
        {
            result.AppendLine($"{indentation}{arrowIndentation} {name} :: {value}");
        }
        else if (value is IEnumerable enumerable && !(value is string))
        {
            AppendCollectionValue(result, name, enumerable, depth, visitedObjects);
        }
        else
        {
            result.AppendLine($"{indentation}{arrowIndentation} {name} :: {{");
            GetStructOrClassMemberValuesRecursive(value, result, depth + 1, visitedObjects);
            result.AppendLine($"{indentation}{arrowIndentation} }}");
        }
    }

    private static void AppendCollectionValue(StringBuilder result, string name, IEnumerable enumerable, int depth, HashSet<object> visitedObjects)
    {
        var indentation = GetIndentation(depth);
        var arrowIndentation = GetArrowIndentation(depth);

        result.AppendLine($"{indentation}{arrowIndentation} {name} :: [");

        var itemIndex = 0;
        foreach (var item in enumerable)
        {
            if (itemIndex > MaxCollectionItems)
            {
                result.AppendLine($"{indentation}    {arrowIndentation} ... [truncated after {MaxCollectionItems} items]");
                break;
            }

            result.AppendLine($"{indentation}    {arrowIndentation} [{itemIndex}]:");
            if (item == null)
            {
                result.AppendLine($"{indentation}    {arrowIndentation} null");
            }
            else if (IsSimpleType(item.GetType()))
            {
                result.AppendLine($"{indentation}    {arrowIndentation} {item}");
            }
            else
            {
                GetStructOrClassMemberValuesRecursive(item, result, depth + 2, visitedObjects);
            }
            itemIndex++;
        }

        result.AppendLine($"{indentation}{arrowIndentation} ]");
    }

    private static string GetIndentation(int depth) => new string(' ', depth * 4);

    private static string GetArrowIndentation(int depth) => depth switch
    {
        0 => "",
        1 => "->",
        2 => "-->",
        3 => "--->",
        _ => new string('-', depth) + ">"
    };

    private static bool IsSimpleType(Type type)
    {
        return type.IsPrimitive ||
               type.IsEnum ||
               PrimitiveTypes.Contains(type) ||
               (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>) &&
                IsSimpleType(type.GetGenericArguments()[0]));
    }

    #endregion

    #region Serialization

    /// <summary>
    /// Convert any class or struct to a JSON string with pretty formatting
    /// </summary>
    public static string ToJson<T>(T obj, bool prettyPrint = true) where T : notnull
    {
        return JsonHelper.Serialize(obj, prettyPrint);
    }

    /// <summary>
    /// Convert any class or struct to XML string
    /// </summary>
    public static string ToXml<T>(T obj) where T : notnull
    {
        try
        {
            var serializer = new XmlSerializer(typeof(T));
            using var writer = new StringWriter();
            serializer.Serialize(writer, obj);
            return writer.ToString();
        }
        catch (Exception ex)
        {
            _logger?.LogException(ex, "XML Serialization");
            return "<error>Failed to serialize</error>";
        }
    }

    #endregion
}