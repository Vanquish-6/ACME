using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ACME.Extractors
{
    /// <summary>
    /// Factory for creating appropriate data extractors based on data types
    /// </summary>
    public class DataExtractorFactory
    {
        private static readonly Lazy<DataExtractorFactory> _instance = new Lazy<DataExtractorFactory>(() => new DataExtractorFactory());
        
        private readonly List<IDataExtractor> _extractors = new List<IDataExtractor>();
        
        /// <summary>
        /// Private constructor to enforce singleton pattern
        /// </summary>
        private DataExtractorFactory()
        {
            // Register all available extractors
            RegisterExtractors();
        }
        
        /// <summary>
        /// Singleton instance
        /// </summary>
        public static DataExtractorFactory Instance => _instance.Value;
        
        /// <summary>
        /// Registers all known extractors
        /// </summary>
        private void RegisterExtractors()
        {
            // Register all available extractors here
            _extractors.Add(new SpellDataExtractor());
            _extractors.Add(new SkillDataExtractor());
            _extractors.Add(new SpellComponentDataExtractor());
            
            Debug.WriteLine($"Registered {_extractors.Count} data extractors");
        }
        
        /// <summary>
        /// Gets an appropriate extractor for the specified data object
        /// </summary>
        public IDataExtractor GetExtractor(object dataObject)
        {
            if (dataObject == null) return null;
            
            string typeName = dataObject.GetType().FullName;
            Debug.WriteLine($"Finding extractor for type: {typeName}");
            
            // Find the first extractor that can handle this type
            foreach (var extractor in _extractors)
            {
                if (extractor.CanExtract(dataObject))
                {
                    Debug.WriteLine($"Using extractor: {extractor.GetType().Name}");
                    return extractor;
                }
            }
            
            Debug.WriteLine("No specific extractor found for this data type");
            return null;
        }
        
        /// <summary>
        /// Extracts data from the given data object using an appropriate extractor
        /// </summary>
        public List<dynamic> ExtractData(object dataObject)
        {
            var extractor = GetExtractor(dataObject);
            
            if (extractor != null)
            {
                return extractor.Extract(dataObject);
            }
            
            Debug.WriteLine("No suitable extractor found, returning empty list");
            return new List<dynamic>();
        }
    }
} 