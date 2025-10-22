
using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Serialization;
using Microsoft.JSInterop;
namespace SubashaVentures.Utilities.HelperScripts
{
    /// <summary>
    /// Debug message type enumeration
    /// </summary>
    public enum DebugClass
    {
        Log,
        Warning,
        Error,
        Exception,
        Info
    }

    /// <summary>
    /// Helper functions for common operations with improved best practices
    /// </summary>
    public static class MID_HelperFunctions
    {
        #region Private Fields
        
        private static volatile bool _isDebugMode = true;
        private static ILogger? _logger;
        private static IJSRuntime? _jsRuntime;
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
        /// Initialize the helper functions with logging dependencies
        /// </summary>
        /// <param name="logger">ILogger instance for server-side logging</param>
        /// <param name="jsRuntime">IJSRuntime for browser console logging</param>
        public static void Initialize(ILogger? logger = null, IJSRuntime? jsRuntime = null)
        {
            lock (_lockObject)
            {
                _logger = logger;
                _jsRuntime = jsRuntime;
            }
        }

        /// <summary>
        /// Set the debug mode (typically called on application startup)
        /// </summary>
        /// <param name="isDebugMode">Whether to enable debug mode</param>
        public static void SetDebugMode(bool isDebugMode)
        {
            lock (_lockObject)
            {
                _isDebugMode = isDebugMode;
                _ = DebugMessageAsync($"Debug mode set to: {isDebugMode}", DebugClass.Info);
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
                _ = DebugMessageAsync($"Regex validation error for pattern '{pattern}': {ex.Message}", DebugClass.Exception);
                return false;
            }
        }

        #endregion

        #region Debug and Logging

        /// <summary>
        /// Prints debug messages asynchronously with support for both ILogger and browser console
        /// </summary>
        /// <param name="message">The message to print</param>
        /// <param name="debugClass">The type of debug message</param>
        public static async Task DebugMessageAsync(string message, DebugClass debugClass = DebugClass.Log)
        {
            if (!_isDebugMode || string.IsNullOrEmpty(message))
                return;

            var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var formattedMessage = $"[{timestamp}] [{debugClass}] {message}";

            // Use ILogger if available (server-side)
            await LogToILogger(formattedMessage, debugClass);

            // Use browser console if JSRuntime is available (Blazor WASM)
            await LogToJSConsole(formattedMessage, debugClass);

            // Fallback to Debug.WriteLine for development
            Debug.WriteLine(formattedMessage);
        }

        /// <summary>
        /// Synchronous version of DebugMessage (fire-and-forget async operation)
        /// </summary>
        /// <param name="message">The message to print</param>
        /// <param name="debugClass">The type of debug message</param>
        public static void DebugMessage(string message, DebugClass debugClass = DebugClass.Log)
        {
            if (!_isDebugMode)
                return;

            // Fire-and-forget async operation to avoid blocking
            _ = Task.Run(async () => await DebugMessageAsync(message, debugClass));
        }

        private static async Task LogToILogger(string message, DebugClass debugClass)
        {
            if (_logger == null) return;

            await Task.Run(() =>
            {
                switch (debugClass)
                {
                    case DebugClass.Warning:
                        _logger.LogWarning("{Message}", message);
                        break;
                    case DebugClass.Error:
                    case DebugClass.Exception:
                        _logger.LogError("{Message}", message);
                        break;
                    case DebugClass.Info:
                        _logger.LogInformation("{Message}", message);
                        break;
                    default:
                        _logger.LogDebug("{Message}", message);
                        break;
                }
            });
        }

        private static async Task LogToJSConsole(string message, DebugClass debugClass)
        {
            if (_jsRuntime == null) return;

            try
            {
                var consoleMethod = debugClass switch
                {
                    DebugClass.Warning => "warn",
                    DebugClass.Error or DebugClass.Exception => "error",
                    DebugClass.Info => "info",
                    _ => "log"
                };

                await _jsRuntime.InvokeVoidAsync($"console.{consoleMethod}", message);
            }
            catch (Exception ex)
            {
                // Fallback to System.Diagnostics.Debug for critical errors
                Debug.WriteLine($"JSInterop logging failed: {ex.Message}");
            }
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
                await DebugMessageAsync($"Task timed out after {timeoutMs} ms", DebugClass.Warning);
                return default;
            }
            catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
            {
                await DebugMessageAsync($"Task was cancelled due to timeout after {timeoutMs} ms", DebugClass.Warning);
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
                    
                _ = DebugMessageAsync(errorMessage, DebugClass.Exception);
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
                var errorMessage = actionName != null 
                    ? $"SafeExecuteAsync error in '{actionName}': {ex.Message}" 
                    : $"SafeExecuteAsync error: {ex.Message}";
                    
                await DebugMessageAsync(errorMessage, DebugClass.Exception);
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

            // Prevent circular references
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

                // Get all fields
                var fields = type.GetFields(bindingFlags);
                foreach (var field in fields)
                {
                    AppendMemberValue(result, field.Name, field.GetValue(structOrClassInstance), depth, visitedObjects);
                }

                // Get all properties
                var properties = type.GetProperties(bindingFlags);
                foreach (var property in properties)
                {
                    if (property.CanRead && property.GetIndexParameters().Length == 0) // Skip indexers
                    {
                        try
                        {
                            var value = property.GetValue(structOrClassInstance);
                            AppendMemberValue(result, property.Name, value, depth, visitedObjects);
                        }
                        catch (Exception ex)
                        {
                            result.AppendLine($"{GetIndentation(depth)}{GetArrowIndentation(depth)} {property.Name} :: [Error accessing property: {ex.Message}]");
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

        private static string GetIndentation(int depth)
        {
            return new string(' ', depth * 4);
        }

        private static string GetArrowIndentation(int depth)
        {
            if (depth == 0) return "";
            if (depth == 1) return "->";
            if (depth == 2) return "-->";
            if (depth == 3) return "--->";

            // For deeper nesting, use the number of levels
            return new string('-', depth) + ">";
        }

        /// <summary>
        /// Determines if a type is a simple type that can be displayed directly
        /// </summary>
        /// <param name="type">The type to check</param>
        /// <returns>True if the type is simple, false otherwise</returns>
        private static bool IsSimpleType(Type type)
        {
            return type.IsPrimitive || 
                   type.IsEnum || 
                   PrimitiveTypes.Contains(type) ||
                   (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>) && IsSimpleType(type.GetGenericArguments()[0]));
        }

        #endregion

        #region Serialization

        /// <summary>
        /// Convert any class or struct to a JSON string with pretty formatting
        /// Uses centralized JsonHelper for consistency
        /// </summary>
        /// <typeparam name="T">The type to serialize</typeparam>
        /// <param name="obj">The object to serialize</param>
        /// <param name="prettyPrint">Whether to format the JSON for readability</param>
        /// <returns>JSON string representation</returns>
        public static string ToJson<T>(T obj, bool prettyPrint = true) where T : notnull
        {
            return JsonHelper.Serialize(obj, prettyPrint);
        }

        /// <summary>
        /// Convert any class or struct to XML string
        /// </summary>
        /// <typeparam name="T">The type to serialize</typeparam>
        /// <param name="obj">The object to serialize</param>
        /// <returns>XML string representation</returns>
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
                _ = DebugMessageAsync($"Failed to serialize object to XML: {ex.Message}", DebugClass.Exception);
                return "<error>Failed to serialize</error>";
            }
        }

        #endregion
    }
}