using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using System.Reflection;

namespace ACME.Extractors
{
    /// <summary>
    /// Base class for data extractors with common extraction utilities
    /// </summary>
    public abstract class BaseDataExtractor : IDataExtractor
    {
        /// <summary>
        /// The property name to use for the value in the extracted items
        /// </summary>
        protected readonly string ValuePropertyName;
        
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="valuePropertyName">The property name to use for the value in the extracted items</param>
        protected BaseDataExtractor(string valuePropertyName)
        {
            ValuePropertyName = valuePropertyName ?? throw new ArgumentNullException(nameof(valuePropertyName));
        }
        
        /// <summary>
        /// Checks if this extractor can handle the specified data object
        /// </summary>
        public abstract bool CanExtract(object dataObject);
        
        /// <summary>
        /// Extracts data from the specified object
        /// </summary>
        public abstract List<dynamic> Extract(object dataObject);
        
        /// <summary>
        /// Creates a dynamic item with the specified ID and value
        /// </summary>
        protected virtual dynamic CreateDynamicItem(object id, object value)
        {
            var item = new ExpandoObject() as IDictionary<string, object>;
            item["Id"] = id;
            item[ValuePropertyName] = value;
            
            // Add a display name combining ID and Name
            string name = "Unknown";
            if (value != null)
            {
                var nameProp = value.GetType().GetProperty("Name");
                if (nameProp != null)
                {
                    var nameValue = nameProp.GetValue(value);
                    if (nameValue != null)
                    {
                        name = nameValue.ToString();
                    }
                }
            }
            item["DisplayName"] = $"{id} - {name}";
            
            return item;
        }
        
        /// <summary>
        /// Extracts data from a dictionary
        /// </summary>
        protected virtual List<dynamic> ExtractFromDictionary(IDictionary dictionary)
        {
            Debug.WriteLine($"Extracting from dictionary with {dictionary.Count} items");
            var items = new List<dynamic>();
            
            foreach (var key in dictionary.Keys)
            {
                var value = dictionary[key];
                items.Add(CreateDynamicItem(key, value));
            }
            
            return items;
        }
        
        /// <summary>
        /// Extracts data from an enumerable
        /// </summary>
        protected virtual List<dynamic> ExtractFromEnumerable(IEnumerable enumerable)
        {
            Debug.WriteLine("Extracting from IEnumerable");
            var items = new List<dynamic>();
            int index = 0;
            
            foreach (var item in enumerable)
            {
                if (item == null) continue;
                
                // Try to extract key/value pairs
                var itemType = item.GetType();
                var keyProp = itemType.GetProperty("Key");
                var valueProp = itemType.GetProperty("Value");
                
                if (keyProp != null && valueProp != null)
                {
                    // It's a key-value pair
                    var key = keyProp.GetValue(item);
                    var value = valueProp.GetValue(item);
                    items.Add(CreateDynamicItem(key, value));
                }
                else
                {
                    // Try to extract ID from the item itself
                    var idProp = itemType.GetProperty("Id") ?? 
                                 itemType.GetProperty("ID");
                    
                    if (idProp != null)
                    {
                        var id = idProp.GetValue(item);
                        items.Add(CreateDynamicItem(id, item));
                    }
                    else
                    {
                        // Use index as ID
                        items.Add(CreateDynamicItem(index, item));
                    }
                }
                
                index++;
            }
            
            return items;
        }
        
        /// <summary>
        /// Tries to extract data from a property of the specified object
        /// </summary>
        protected virtual List<dynamic> TryExtractFromProperty(object dataObject, string propertyName)
        {
            try
            {
                var property = dataObject.GetType().GetProperty(propertyName);
                if (property != null && property.CanRead)
                {
                    var value = property.GetValue(dataObject);
                    if (value != null)
                    {
                        Debug.WriteLine($"Found property {propertyName} of type {value.GetType().FullName}");
                        
                        if (value is IDictionary dict)
                        {
                            return ExtractFromDictionary(dict);
                        }
                        else if (value is IEnumerable enumerable && !(value is string))
                        {
                            return ExtractFromEnumerable(enumerable);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error extracting from property {propertyName}: {ex.Message}");
            }
            
            return new List<dynamic>();
        }
        
        /// <summary>
        /// Tries to extract data from a method of the specified object
        /// </summary>
        protected virtual List<dynamic> TryExtractFromMethod(object dataObject, string methodName)
        {
            try
            {
                var method = dataObject.GetType().GetMethod(methodName, Type.EmptyTypes);
                if (method != null)
                {
                    Debug.WriteLine($"Invoking method {methodName}");
                    var result = method.Invoke(dataObject, null);
                    if (result != null)
                    {
                        Debug.WriteLine($"Method {methodName} returned {result.GetType().FullName}");
                        
                        if (result is IDictionary dict)
                        {
                            return ExtractFromDictionary(dict);
                        }
                        else if (result is IEnumerable enumerable && !(result is string))
                        {
                            return ExtractFromEnumerable(enumerable);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error extracting from method {methodName}: {ex.Message}");
            }
            
            return new List<dynamic>();
        }
        
        /// <summary>
        /// Tries to extract data from a field of the specified object
        /// </summary>
        protected virtual List<dynamic> TryExtractFromFields(object dataObject)
        {
            try
            {
                var fields = dataObject.GetType().GetFields(BindingFlags.NonPublic | 
                                                          BindingFlags.Instance | 
                                                          BindingFlags.Public);
                
                foreach (var field in fields)
                {
                    var value = field.GetValue(dataObject);
                    if (value != null)
                    {
                        Debug.WriteLine($"Field {field.Name} is type {value.GetType().FullName}");
                        
                        if (value is IDictionary dict)
                        {
                            var items = ExtractFromDictionary(dict);
                            if (items.Count > 0)
                            {
                                Debug.WriteLine($"Extracted {items.Count} items from field {field.Name}");
                                return items;
                            }
                        }
                        else if (value is IEnumerable enumerable && !(value is string))
                        {
                            // Check if enumerable has items
                            var hasItems = false;
                            foreach (var item in enumerable)
                            {
                                if (item != null)
                                {
                                    hasItems = true;
                                    break;
                                }
                            }
                            
                            if (hasItems)
                            {
                                var items = ExtractFromEnumerable(enumerable);
                                if (items.Count > 0)
                                {
                                    Debug.WriteLine($"Extracted {items.Count} items from field {field.Name}");
                                    return items;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error extracting from fields: {ex.Message}");
            }
            
            return new List<dynamic>();
        }
    }
} 