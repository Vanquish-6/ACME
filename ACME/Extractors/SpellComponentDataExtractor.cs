using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;

namespace ACME.Extractors
{
    /// <summary>
    /// Specialized extractor for spell component data
    /// </summary>
    public class SpellComponentDataExtractor : BaseDataExtractor
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public SpellComponentDataExtractor() : base("ComponentValue")
        {
        }
        
        /// <summary>
        /// Checks if this extractor can handle the specified data object
        /// </summary>
        public override bool CanExtract(object dataObject)
        {
            if (dataObject == null) return false;
            
            // Check if it's the expected SpellComponentsTable type
            var typeName = dataObject.GetType().FullName;
            return typeName == "DatReaderWriter.DBObjs.SpellComponentsTable";
        }
        
        /// <summary>
        /// Extracts data from the specified object
        /// </summary>
        public override List<dynamic> Extract(object dataObject)
        {
            if (dataObject == null) return new List<dynamic>();
            
            Debug.WriteLine($"Extracting spell components from {dataObject.GetType().FullName}");
            
            // Try property approaches first
            var items = TryExtractFromProperty(dataObject, "Components");
            if (items.Count > 0) return items;
            
            // Try method approaches
            items = TryExtractFromMethod(dataObject, "GetComponents");
            if (items.Count > 0) return items;
            
            // Try table property
            items = TryExtractFromProperty(dataObject, "Table");
            if (items.Count > 0) return items;
            
            // Try fields as a last resort
            items = TryExtractFromFields(dataObject);
            
            return items;
        }
        
        /// <summary>
        /// Override the create dynamic item to add special handling for spell components
        /// </summary>
        protected override ExpandoObject CreateDynamicItem(object id, object value)
        {
            var item = new ExpandoObject() as IDictionary<string, object>;
            
            // Add standard ID property
            item["ID"] = id;
            item[ValuePropertyName] = value;
            
            // Try to get name from the value object
            string name = "Unknown";
            try
            {
                var nameProperty = value?.GetType().GetProperty("Name");
                if (nameProperty != null)
                {
                    var nameObj = nameProperty.GetValue(value);
                    if (nameObj != null)
                    {
                        name = nameObj.ToString();
                    }
                }
                else
                {
                    // Try component type property
                    var typeProperty = value?.GetType().GetProperty("ComponentType");
                    if (typeProperty != null)
                    {
                        var typeObj = typeProperty.GetValue(value);
                        if (typeObj != null)
                        {
                            name = typeObj.ToString();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting component name: {ex.Message}");
            }
            
            // Add display name that combines ID and name
            item["DisplayName"] = $"{id} - {name}";
            
            return (ExpandoObject)item;
        }
    }
} 