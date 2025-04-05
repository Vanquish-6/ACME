using System;

namespace ACME.Models
{
    /// <summary>
    /// Helper class to store TreeView node data
    /// </summary>
    public class TreeNodeData
    {
        /// <summary>
        /// The display name for the node in the TreeView
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;
        
        /// <summary>
        /// Identifier for the node, can be a uint File ID or string tag
        /// </summary>
        public object? Identifier { get; set; }
    }
} 