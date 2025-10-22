
using System.Xml.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
namespace SubashaVentures.Utilities.HelperScripts
{
    /// <summary>
    /// Helper class for JSON operations using Newtonsoft.Json
    /// </summary>
    public static class JsonHelper
    {
        #region JSON Serialization

        /// <summary>
        /// Serialize an object to JSON string
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

                var settings = new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                    Formatting = indented ? Newtonsoft.Json.Formatting.Indented : Newtonsoft.Json.Formatting.None,
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                    DateFormatHandling = DateFormatHandling.IsoDateFormat
                };

                return JsonConvert.SerializeObject(obj, settings);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"JSON serialization error: {ex.Message}", DebugClass.Exception);
                return null;
            }
        }

        /// <summary>
        /// Serialize an object to JSON string with camelCase property names
        /// </summary>
        /// <typeparam name="T">The type of object to serialize</typeparam>
        /// <param name="obj">The object to serialize</param>
        /// <param name="indented">Whether to format the JSON with indentation</param>
        /// <returns>JSON string or null if serialization fails</returns>
        public static string SerializeCamelCase<T>(T obj, bool indented = false)
        {
            try
            {
                if (obj == null)
                    return null;

                var settings = new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                    Formatting = indented ? Newtonsoft.Json.Formatting.Indented : Newtonsoft.Json.Formatting.None,
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                };

                return JsonConvert.SerializeObject(obj, settings);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"JSON camelCase serialization error: {ex.Message}", DebugClass.Exception);
                return null;
            }
        }

        #endregion

        #region JSON Deserialization

        /// <summary>
        /// Deserialize a JSON string to an object
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

                var settings = new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                    DateFormatHandling = DateFormatHandling.IsoDateFormat
                };

                return JsonConvert.DeserializeObject<T>(json, settings);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"JSON deserialization error: {ex.Message}", DebugClass.Exception);
                return default;
            }
        }

        /// <summary>
        /// Deserialize a JSON string to a dynamic object
        /// </summary>
        /// <param name="json">The JSON string to deserialize</param>
        /// <returns>The deserialized dynamic object or null if deserialization fails</returns>
        public static dynamic DeserializeDynamic(string json)
        {
            try
            {
                if (!MID_HelperFunctions.IsValidString(json))
                    return null;

                return JsonConvert.DeserializeObject(json);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"JSON dynamic deserialization error: {ex.Message}", DebugClass.Exception);
                return null;
            }
        }

        #endregion

        #region JSON Conversion

        /// <summary>
        /// Convert JSON to XML
        /// </summary>
        /// <param name="json">The JSON string to convert</param>
        /// <param name="rootElementName">The name of the root XML element</param>
        /// <returns>XML string or null if conversion fails</returns>
        public static string JsonToXml(string json, string rootElementName = "root")
        {
            try
            {
                if (!MID_HelperFunctions.IsValidString(json))
                    return null;

                var jsonObject = JsonConvert.DeserializeObject(json);
                var xmlDoc = new XDocument();
                var rootElement = new XElement(rootElementName);
                xmlDoc.Add(rootElement);
                
                ConvertJsonToXml(jsonObject, rootElement);
                
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
                var jsonObject = new JObject();
                
                foreach (var element in xmlDoc.Root.Elements())
                {
                    ConvertXmlToJson(element, jsonObject);
                }
                
                return jsonObject.ToString(indented ? Newtonsoft.Json.Formatting.Indented : Newtonsoft.Json.Formatting.None);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"XML to JSON conversion error: {ex.Message}", DebugClass.Exception);
                return null;
            }
        }

      
        #endregion

        #region Helper Methods

        /// <summary>
        /// Convert JSON object to XML recursively
        /// </summary>
        private static void ConvertJsonToXml(object jsonObject, XElement parentElement)
        {
            if (jsonObject is JObject jObject)
            {
                foreach (var property in jObject.Properties())
                {
                    if (property.Value is JValue jValue)
                    {
                        var element = new XElement(property.Name);
                        if (jValue.Value != null)
                        {
                            element.Value = jValue.Value.ToString();
                        }
                        parentElement.Add(element);
                    }
                    else if (property.Value is JObject childObject)
                    {
                        var element = new XElement(property.Name);
                        parentElement.Add(element);
                        ConvertJsonToXml(childObject, element);
                    }
                    else if (property.Value is JArray jArray)
                    {
                        var element = new XElement(property.Name);
                        parentElement.Add(element);
                        ConvertJsonToXml(jArray, element);
                    }
                }
            }
            else if (jsonObject is JArray jArray)
            {
                foreach (var item in jArray)
                {
                    var element = new XElement("item");
                    parentElement.Add(element);
                    ConvertJsonToXml(item, element);
                }
            }
        }

        /// <summary>
        /// Convert XML element to JSON recursively
        /// </summary>
        private static void ConvertXmlToJson(XElement element, JObject parentObject)
        {
            if (!element.HasElements)
            {
                parentObject[element.Name.LocalName] = element.Value;
            }
            else
            {
                var childObject = new JObject();
                foreach (var childElement in element.Elements())
                {
                    ConvertXmlToJson(childElement, childObject);
                }
                parentObject[element.Name.LocalName] = childObject;
            }
        }

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

                JToken.Parse(json);
                return true;
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"JSON validation error: {ex.Message}", DebugClass.Exception);
                return false;
            }
        }

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

                var obj1 = JObject.Parse(json1);
                var obj2 = JObject.Parse(json2);
                
                obj1.Merge(obj2, new JsonMergeSettings { 
                    MergeArrayHandling = MergeArrayHandling.Union,
                    MergeNullValueHandling = MergeNullValueHandling.Merge
                });
                
                return obj1.ToString(indented ? Newtonsoft.Json.Formatting.Indented : Newtonsoft.Json.Formatting.None);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"JSON merge error: {ex.Message}", DebugClass.Exception);
                return null;
            }
        }

        #endregion
    }
}