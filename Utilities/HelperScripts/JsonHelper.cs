
using System.Xml.Linq;
using System.Xml.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SubashaVentures.Utilities.HelperScripts
{
    /// <summary>
    /// Helper class for JSON operations using System.Text.Json
    /// Centralized JSON configuration for consistent serialization across Firebase and Supabase
    /// </summary>
    public static class JsonHelper
    {
        #region JSON Options Configuration

        /// <summary>
        /// Default JSON serializer options used across the application
        /// Ensures consistency between Firebase and Supabase operations
        /// </summary>
        public static readonly JsonSerializerOptions DefaultOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };

        /// <summary>
        /// Indented JSON options for readable output (debugging, logging)
        /// </summary>
        public static readonly JsonSerializerOptions IndentedOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };

        /// <summary>
        /// Strict JSON options for API compatibility
        /// </summary>
        public static readonly JsonSerializerOptions StrictOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            WriteIndented = false
        };

        #endregion

        #region JSON Serialization

        /// <summary>
        /// Serialize an object to JSON string using default options
        /// </summary>
        /// <typeparam name="T">The type of object to serialize</typeparam>
        /// <param name="obj">The object to serialize</param>
        /// <param name="indented">Whether to format the JSON with indentation</param>
        /// <returns>JSON string or null if serialization fails</returns>
        public static string Serialize<T>(T obj, bool indented = false)
        {
            try
            {
                if (obj == null)
                    return null;

                var options = indented ? IndentedOptions : DefaultOptions;
                return JsonSerializer.Serialize(obj, options);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"JSON serialization error: {ex.Message}", DebugClass.Exception);
                return null;
            }
        }

        /// <summary>
        /// Serialize an object to JSON string with custom options
        /// </summary>
        /// <typeparam name="T">The type of object to serialize</typeparam>
        /// <param name="obj">The object to serialize</param>
        /// <param name="options">Custom JSON serializer options</param>
        /// <returns>JSON string or null if serialization fails</returns>
        public static string SerializeWithOptions<T>(T obj, JsonSerializerOptions options)
        {
            try
            {
                if (obj == null)
                    return null;

                return JsonSerializer.Serialize(obj, options);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"JSON serialization error: {ex.Message}", DebugClass.Exception);
                return null;
            }
        }

        #endregion

        #region JSON Deserialization

        /// <summary>
        /// Deserialize a JSON string to an object using default options
        /// </summary>
        /// <typeparam name="T">The type to deserialize to</typeparam>
        /// <param name="json">The JSON string to deserialize</param>
        /// <returns>The deserialized object or default if deserialization fails</returns>
        public static T Deserialize<T>(string json)
        {
            try
            {
                if (!MID_HelperFunctions.IsValidString(json))
                    return default;

                return JsonSerializer.Deserialize<T>(json, DefaultOptions);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"JSON deserialization error: {ex.Message}", DebugClass.Exception);
                return default;
            }
        }

        /// <summary>
        /// Deserialize a JSON string to an object with custom options
        /// </summary>
        /// <typeparam name="T">The type to deserialize to</typeparam>
        /// <param name="json">The JSON string to deserialize</param>
        /// <param name="options">Custom JSON serializer options</param>
        /// <returns>The deserialized object or default if deserialization fails</returns>
        public static T DeserializeWithOptions<T>(string json, JsonSerializerOptions options)
        {
            try
            {
                if (!MID_HelperFunctions.IsValidString(json))
                    return default;

                return JsonSerializer.Deserialize<T>(json, options);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"JSON deserialization error: {ex.Message}", DebugClass.Exception);
                return default;
            }
        }

        /// <summary>
        /// Deserialize a JSON string to a JsonDocument for dynamic access
        /// </summary>
        /// <param name="json">The JSON string to deserialize</param>
        /// <returns>The deserialized JsonDocument or null if deserialization fails</returns>
        public static JsonDocument DeserializeToDocument(string json)
        {
            try
            {
                if (!MID_HelperFunctions.IsValidString(json))
                    return null;

                return JsonDocument.Parse(json);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"JSON document deserialization error: {ex.Message}", DebugClass.Exception);
                return null;
            }
        }

        #endregion

        #region JSON Validation

        /// <summary>
        /// Validate if a string is valid JSON
        /// </summary>
        /// <param name="json">The JSON string to validate</param>
        /// <returns>True if valid JSON, false otherwise</returns>
        public static bool IsValidJson(string json)
        {
            try
            {
                if (!MID_HelperFunctions.IsValidString(json))
                    return false;

                using var doc = JsonDocument.Parse(json);
                return true;
            }
            catch (JsonException ex)
            {
                MID_HelperFunctions.DebugMessage($"JSON validation error: {ex.Message}", DebugClass.Exception);
                return false;
            }
        }

        #endregion

        #region JSON Conversion

        /// <summary>
        /// Convert JSON to XML
        /// </summary>
        /// <param name="json">The JSON string to convert</param>
        /// <param name="rootElementName">The name of the root XML element/// <returns>XML string or null if conversion fails</returns>
        public static string JsonToXml(string json, string rootElementName = "root")
        {
            try
            {
                if (!MID_HelperFunctions.IsValidString(json))
                    return null;

                using var jsonDoc = JsonDocument.Parse(json);
                var xmlDoc = new XDocument();
                var rootElement = new XElement(rootElementName);
                xmlDoc.Add(rootElement);
                
                ConvertJsonToXml(jsonDoc.RootElement, rootElement);
                
                return xmlDoc.ToString();
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"JSON to XML conversion error: {ex.Message}", DebugClass.Exception);
                return null;
            }
        }

        /// <summary>
        /// Convert XML to JSON
        /// </summary>
        /// <param name="xml">The XML string to convert</param>
        /// <param name="indented">Whether to format the JSON with indentation</param>
        /// <returns>JSON string or null if conversion fails</returns>
        public static string XmlToJson(string xml, bool indented = false)
        {
            try
            {
                if (!MID_HelperFunctions.IsValidString(xml))
                    return null;

                var xmlDoc = XDocument.Parse(xml);
                var jsonObject = new Dictionary<string, object>();
                
                foreach (var element in xmlDoc.Root.Elements())
                {
                    ConvertXmlToJson(element, jsonObject);
                }

                var options = indented ? IndentedOptions : DefaultOptions;
                return JsonSerializer.Serialize(jsonObject, options);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"XML to JSON conversion error: {ex.Message}", DebugClass.Exception);
                return null;
            }
        }

        #endregion

        #region JSON Manipulation

        /// <summary>
        /// Merge two JSON objects
        /// </summary>
        /// <param name="json1">The first JSON string</param>
        /// <param name="json2">The second JSON string</param>
        /// <param name="indented">Whether to format the result with indentation</param>
        /// <returns>Merged JSON string or null if merge fails</returns>
        public static string MergeJson(string json1, string json2, bool indented = false)
        {
            try
            {
                if (!IsValidJson(json1) || !IsValidJson(json2))
                    return null;

                using var doc1 = JsonDocument.Parse(json1);
                using var doc2 = JsonDocument.Parse(json2);

                var merged = new Dictionary<string, object>();
                
                // Add properties from first JSON
                foreach (var property in doc1.RootElement.EnumerateObject())
                {
                    merged[property.Name] = ConvertJsonElement(property.Value);
                }
                
                // Merge properties from second JSON (overwrites if exists)
                foreach (var property in doc2.RootElement.EnumerateObject())
                {
                    merged[property.Name] = ConvertJsonElement(property.Value);
                }

                var options = indented ? IndentedOptions : DefaultOptions;
                return JsonSerializer.Serialize(merged, options);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"JSON merge error: {ex.Message}", DebugClass.Exception);
                return null;
            }
        }

        /// <summary>
        /// Clone a JSON object
        /// </summary>
        /// <typeparam name="T">The type to clone</typeparam>
        /// <param name="obj">The object to clone</param>
        /// <returns>Cloned object or default if cloning fails</returns>
        public static T Clone<T>(T obj)
        {
            try
            {
                if (obj == null)
                    return default;

                var json = Serialize(obj);
                return Deserialize<T>(json);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"JSON clone error: {ex.Message}", DebugClass.Exception);
                return default;
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Convert JsonElement to XML recursively
        /// </summary>
        private static void ConvertJsonToXml(JsonElement element, XElement parentElement)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var property in element.EnumerateObject())
                    {
                        var childElement = new XElement(property.Name);
                        parentElement.Add(childElement);
                        ConvertJsonToXml(property.Value, childElement);
                    }
                    break;

                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                    {
                        var itemElement = new XElement("item");
                        parentElement.Add(itemElement);
                        ConvertJsonToXml(item, itemElement);
                    }
                    break;

                case JsonValueKind.String:
                    parentElement.Value = element.GetString() ?? string.Empty;
                    break;

                case JsonValueKind.Number:
                    parentElement.Value = element.GetRawText();
                    break;

                case JsonValueKind.True:
                case JsonValueKind.False:
                    parentElement.Value = element.GetBoolean().ToString().ToLowerInvariant();
                    break;

                case JsonValueKind.Null:
                    parentElement.SetAttributeValue("null", "true");
                    break;
            }
        }

        /// <summary>
        /// Convert XML element to JSON recursively
        /// </summary>
        private static void ConvertXmlToJson(XElement element, Dictionary<string, object> parentObject)
        {
            if (!element.HasElements)
            {
                parentObject[element.Name.LocalName] = element.Value;
            }
            else
            {
                var childObject = new Dictionary<string, object>();
                foreach (var childElement in element.Elements())
                {
                    ConvertXmlToJson(childElement, childObject);
                }
                parentObject[element.Name.LocalName] = childObject;
            }
        }

        /// <summary>
        /// Convert JsonElement to object for manipulation
        /// </summary>
        private static object ConvertJsonElement(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    var dict = new Dictionary<string, object>();
                    foreach (var property in element.EnumerateObject())
                    {
                        dict[property.Name] = ConvertJsonElement(property.Value);
                    }
                    return dict;

                case JsonValueKind.Array:
                    var list = new List<object>();
                    foreach (var item in element.EnumerateArray())
                    {
                        list.Add(ConvertJsonElement(item));
                    }
                    return list;

                case JsonValueKind.String:
                    return element.GetString();

                case JsonValueKind.Number:
                    if (element.TryGetInt32(out int intValue))
                        return intValue;
                    if (element.TryGetInt64(out long longValue))
                        return longValue;
                    if (element.TryGetDouble(out double doubleValue))
                        return doubleValue;
                    return element.GetDecimal();

                case JsonValueKind.True:
                    return true;

                case JsonValueKind.False:
                    return false;

                case JsonValueKind.Null:
                    return null;

                default:
                    return element.GetRawText();
            }
        }

        #endregion

        #region Compatibility Methods

        /// <summary>
        /// Get a JSON property value safely
        /// </summary>
        /// <param name="json">The JSON string</param>
        /// <param name="propertyName">The property name to retrieve</param>
        /// <returns>The property value or null if not found</returns>
        public static string GetPropertyValue(string json, string propertyName)
        {
            try
            {
                if (!IsValidJson(json))
                    return null;

                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty(propertyName, out var property))
                {
                    return property.GetRawText();
                }

                return null;
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error getting property value: {ex.Message}", DebugClass.Exception);
                return null;
            }
        }

        /// <summary>
        /// Set a JSON property value
        /// </summary>
        /// <param name="json">The JSON string</param>
        /// <param name="propertyName">The property name to set</param>
        /// <param name="value">The value to set</param>
        /// <returns>Updated JSON string or original if operation fails</returns>
        public static string SetPropertyValue(string json, string propertyName, object value)
        {
            try
            {
                if (!IsValidJson(json))
                    return json;

                var dict = Deserialize<Dictionary<string, object>>(json);
                if (dict != null)
                {
                    dict[propertyName] = value;
                    return Serialize(dict);
                }

                return json;
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error setting property value: {ex.Message}", DebugClass.Exception);
                return json;
            }
        }

        #endregion
    }
}