using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;

namespace ACME.Extractors
{
    /// <summary>
    /// Specialized extractor for skill data
    /// </summary>
    public class SkillDataExtractor : BaseDataExtractor
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public SkillDataExtractor() : base("SkillValue")
        {
        }
        
        /// <summary>
        /// Checks if this extractor can handle the specified data object
        /// </summary>
        public override bool CanExtract(object dataObject)
        {
            if (dataObject == null) return false;
            
            // Check if it's the expected SkillTable type
            var typeName = dataObject.GetType().FullName;
            return typeName == "DatReaderWriter.DBObjs.SkillTable";
        }
        
        /// <summary>
        /// Extracts data from the specified object
        /// </summary>
        public override List<dynamic> Extract(object dataObject)
        {
            if (dataObject == null) return new List<dynamic>();
            
            Debug.WriteLine($"Extracting skills from {dataObject.GetType().FullName}");
            
            // Try property approaches first
            var items = TryExtractFromProperty(dataObject, "Skills");
            if (items.Count > 0) return items;
            
            // Try method approaches
            items = TryExtractFromMethod(dataObject, "GetSkills");
            if (items.Count > 0) return items;
            
            // Try dictionary-based extractor
            items = TryExtractFromDictionary(dataObject);
            if (items.Count > 0) return items;
            
            // Try fields as a last resort
            items = TryExtractFromFields(dataObject);
            
            return items;
        }
        
        /// <summary>
        /// Tries to extract data from a dictionary-based property or field
        /// </summary>
        private List<dynamic> TryExtractFromDictionary(object dataObject)
        {
            var items = new List<dynamic>();
            
            try
            {
                // Look for dictionary-like properties
                var dictProperties = dataObject.GetType().GetProperties()
                    .Where(p => p.PropertyType.Name.Contains("Dictionary"))
                    .ToList();
                
                foreach (var prop in dictProperties)
                {
                    try
                    {
                        var dictObj = prop.GetValue(dataObject);
                        if (dictObj == null) continue;
                        
                        // Get the dictionary entries
                        var entriesMethod = dictObj.GetType().GetMethod("GetEnumerator");
                        if (entriesMethod != null)
                        {
                            var enumerator = entriesMethod.Invoke(dictObj, null);
                            var moveNextMethod = enumerator.GetType().GetMethod("MoveNext");
                            var currentProperty = enumerator.GetType().GetProperty("Current");
                            
                            while ((bool)moveNextMethod.Invoke(enumerator, null))
                            {
                                var current = currentProperty.GetValue(enumerator);
                                var keyProp = current.GetType().GetProperty("Key");
                                var valueProp = current.GetType().GetProperty("Value");
                                
                                if (keyProp != null && valueProp != null)
                                {
                                    var key = keyProp.GetValue(current);
                                    var value = valueProp.GetValue(current);
                                    items.Add(CreateDynamicItem(key, value));
                                }
                            }
                            
                            if (items.Count > 0)
                            {
                                Debug.WriteLine($"Extracted {items.Count} items from dictionary property {prop.Name}");
                                return items;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error extracting from dictionary property {prop.Name}: {ex.Message}");
                    }
                }
                
                // Look for dictionary-like fields
                var dictFields = dataObject.GetType().GetFields()
                    .Where(f => f.FieldType.Name.Contains("Dictionary"))
                    .ToList();
                
                foreach (var field in dictFields)
                {
                    try
                    {
                        var dictObj = field.GetValue(dataObject);
                        if (dictObj == null) continue;
                        
                        // Try to extract from this dictionary
                        var entriesMethod = dictObj.GetType().GetMethod("GetEnumerator");
                        if (entriesMethod != null)
                        {
                            var enumerator = entriesMethod.Invoke(dictObj, null);
                            var moveNextMethod = enumerator.GetType().GetMethod("MoveNext");
                            var currentProperty = enumerator.GetType().GetProperty("Current");
                            
                            while ((bool)moveNextMethod.Invoke(enumerator, null))
                            {
                                var current = currentProperty.GetValue(enumerator);
                                var keyProp = current.GetType().GetProperty("Key");
                                var valueProp = current.GetType().GetProperty("Value");
                                
                                if (keyProp != null && valueProp != null)
                                {
                                    var key = keyProp.GetValue(current);
                                    var value = valueProp.GetValue(current);
                                    items.Add(CreateDynamicItem(key, value));
                                }
                            }
                            
                            if (items.Count > 0)
                            {
                                Debug.WriteLine($"Extracted {items.Count} items from dictionary field {field.Name}");
                                return items;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error extracting from dictionary field {field.Name}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in TryExtractFromDictionary: {ex.Message}");
            }
            
            return items;
        }
    }
} 