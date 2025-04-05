using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;

namespace ACME.Extractors
{
    /// <summary>
    /// Specialized extractor for spell data
    /// </summary>
    public class SpellDataExtractor : BaseDataExtractor
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public SpellDataExtractor() : base("SpellValue")
        {
            Debug.WriteLine("SpellDataExtractor initialized with property name 'SpellValue'");
        }
        
        /// <summary>
        /// Checks if this extractor can handle the specified data object
        /// </summary>
        public override bool CanExtract(object dataObject)
        {
            if (dataObject == null) return false;
            
            // Check if it's the expected SpellTable type
            var typeName = dataObject.GetType().FullName;
            return typeName == "DatReaderWriter.DBObjs.SpellTable";
        }
        
        /// <summary>
        /// Extracts data from the specified object
        /// </summary>
        public override List<dynamic> Extract(object dataObject)
        {
            if (dataObject == null) return new List<dynamic>();
            
            Debug.WriteLine($"Extracting spells from {dataObject.GetType().FullName}");
            
            // Special direct extraction method if it's the expected type
            if (dataObject is DatReaderWriter.DBObjs.SpellTable spellTable)
            {
                Debug.WriteLine($"Using direct extraction for SpellTable with {spellTable.Spells.Count} spells");
                var result = new List<dynamic>();
                
                // Extract directly from the Dictionary<uint, SpellBase>
                foreach (var kvp in spellTable.Spells)
                {
                    var item = new ExpandoObject() as IDictionary<string, object>;
                    item["Id"] = kvp.Key;
                    item["SpellValue"] = kvp.Value;
                    item["DisplayName"] = $"{kvp.Key} - {kvp.Value.Name}";
                    result.Add(item);
                }
                
                Debug.WriteLine($"Direct extraction yielded {result.Count} items");
                return result;
            }
            
            // Original implementation as fallback
            Debug.WriteLine("Falling back to original extraction methods");
            
            // Try property approaches first
            var items = TryExtractFromProperty(dataObject, "Spells");
            if (items.Count > 0) return items;
            
            // Try method approaches
            items = TryExtractFromMethod(dataObject, "GetSpells");
            if (items.Count > 0) return items;
            
            // Try indexer if available
            items = TryExtractFromIndexer(dataObject);
            if (items.Count > 0) return items;
            
            // Try fields as a last resort
            items = TryExtractFromFields(dataObject);
            if (items.Count > 0) return items;
            
            // Final attempt - look for collection-like properties
            items = TryExtractFromCollectionProperties(dataObject);
            
            return items;
        }
        
        /// <summary>
        /// Tries to extract data using indexer methods
        /// </summary>
        private List<dynamic> TryExtractFromIndexer(object dataObject)
        {
            var items = new List<dynamic>();
            
            try
            {
                // Look for indexer method
                var itemGetMethod = dataObject.GetType().GetMethod("get_Item", new[] { typeof(int) }) ??
                                    dataObject.GetType().GetMethod("get_Item", new[] { typeof(uint) });
                
                if (itemGetMethod != null)
                {
                    Debug.WriteLine("Found indexer method, attempting to enumerate spells");
                    
                    // Try to get the count of items
                    var countProperty = dataObject.GetType().GetProperty("Count");
                    int countToTry = 5000; // Default max count to try
                    
                    if (countProperty != null)
                    {
                        var countObj = countProperty.GetValue(dataObject);
                        if (countObj is int intCount)
                        {
                            countToTry = intCount;
                            Debug.WriteLine($"Count property indicates {countToTry} items");
                        }
                    }
                    
                    // Try to access items by index
                    for (int i = 0; i < countToTry; i++)
                    {
                        try
                        {
                            var spellObj = itemGetMethod.Invoke(dataObject, new object[] { i });
                            if (spellObj != null)
                            {
                                items.Add(CreateDynamicItem(i, spellObj));
                                
                                // After we find a few items, limit the search to reduce time
                                if (items.Count > 10 && countToTry > 100)
                                {
                                    countToTry = 100;
                                }
                            }
                        }
                        catch
                        {
                            // If we get an exception, we've likely exceeded bounds
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error extracting using indexer: {ex.Message}");
            }
            
            return items;
        }
        
        /// <summary>
        /// Tries to extract data from collection-like properties
        /// </summary>
        private List<dynamic> TryExtractFromCollectionProperties(object dataObject)
        {
            var tableProperties = dataObject.GetType().GetProperties()
                .Where(p => p.PropertyType.Name.Contains("Dictionary") || 
                            p.PropertyType.Name.Contains("Collection") || 
                            p.PropertyType.Name.Contains("List") ||
                            (p.PropertyType.GetInterfaces().Any(i => i.Name.Contains("IEnumerable")) &&
                             !p.PropertyType.Name.Contains("String")))
                .ToList();
            
            Debug.WriteLine($"Found {tableProperties.Count} potential collection properties");
            
            foreach (var prop in tableProperties)
            {
                var items = TryExtractFromProperty(dataObject, prop.Name);
                if (items.Count > 0)
                {
                    Debug.WriteLine($"Extracted {items.Count} items from property {prop.Name}");
                    return items;
                }
            }
            
            return new List<dynamic>();
        }
    }
} 