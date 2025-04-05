using System.Collections.Generic;
using System.Dynamic;

namespace ACME.Extractors
{
    /// <summary>
    /// Interface for data extractors that can transform database objects into UI-friendly collections
    /// </summary>
    public interface IDataExtractor
    {
        /// <summary>
        /// Checks if this extractor can handle the specified data object
        /// </summary>
        /// <param name="dataObject">The data object to check</param>
        /// <returns>True if this extractor can handle the data object, false otherwise</returns>
        bool CanExtract(object dataObject);
        
        /// <summary>
        /// Extracts data from the specified object
        /// </summary>
        /// <param name="dataObject">The data object to extract from</param>
        /// <returns>A list of dynamic objects containing the extracted data</returns>
        List<dynamic> Extract(object dataObject);
    }
} 