using System;

namespace ACME.Models
{
    /// <summary>
    /// Helper class to store both file ID and database ID for TreeView nodes
    /// </summary>
    public class NodeIdentifier
    {
        /// <summary>
        /// The file ID in the DAT database
        /// </summary>
        public uint FileId { get; set; }
        
        /// <summary>
        /// The database ID to identify which loaded database to use
        /// </summary>
        public string DatabaseId { get; set; } = string.Empty;
        
        /// <summary>
        /// For further specifying subtypes within a file, such as "Spells" vs "SpellSets"
        /// </summary>
        public string Subtype { get; set; } = string.Empty; 
        
        public override string ToString()
        {
            if (!string.IsNullOrEmpty(Subtype))
                return $"FileId: 0x{FileId:X8}, DatabaseId: {DatabaseId}, Subtype: {Subtype}";
            else
                return $"FileId: 0x{FileId:X8}, DatabaseId: {DatabaseId}";
        }
    }
} 