using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using DatReaderWriter;
using Microsoft.UI.Xaml.Controls;
using ACME.Constants;
using ACME.Models;

namespace ACME.Managers
{
    /// <summary>
    /// Manager class for handling TreeView population and management
    /// </summary>
    public class TreeViewManager
    {
        private readonly TreeView _treeView;
        
        public TreeViewManager(TreeView treeView)
        {
            _treeView = treeView ?? throw new ArgumentNullException(nameof(treeView));
        }
        
        /// <summary>
        /// Populate the TreeView with Portal database content
        /// </summary>
        /// <param name="portalDb">The Portal database</param>
        /// <param name="fileName">The filename of the database</param>
        /// <param name="dbId">The database ID</param>
        public void PopulatePortalTreeView(PortalDatabase portalDb, string fileName, string dbId)
        {
            Debug.WriteLine("Populating Portal TreeView...");

            var portalRoot = new TreeViewNode()
            {
                Content = new TreeNodeData 
                { 
                    DisplayName = $"Portal Data - {fileName}", 
                    Identifier = $"Root_Portal_{dbId}" 
                },
                IsExpanded = true // Ensure root is expanded
            };
            
            Debug.WriteLine($"Created Root Node: {((TreeNodeData)portalRoot.Content).DisplayName}");

            // Add SpellTable with children for Spells and SpellSets
            var spellTableNode = new TreeViewNode()
            {
                Content = new TreeNodeData 
                { 
                    DisplayName = "SpellTable", 
                    Identifier = new NodeIdentifier { FileId = DatFileIds.SpellTableId, DatabaseId = dbId }
                },
                IsExpanded = false
            };
            
            // Add child nodes for Spells and SpellSets
            var spellsNode = new TreeViewNode()
            {
                Content = new TreeNodeData 
                { 
                    DisplayName = "Spells", 
                    Identifier = new NodeIdentifier { FileId = DatFileIds.SpellTableId, DatabaseId = dbId, Subtype = "Spells" }
                }
            };
            spellTableNode.Children.Add(spellsNode);
            
            var spellSetsNode = new TreeViewNode()
            {
                Content = new TreeNodeData 
                { 
                    DisplayName = "Spell Sets", 
                    Identifier = new NodeIdentifier { FileId = DatFileIds.SpellTableId, DatabaseId = dbId, Subtype = "SpellSets" }
                }
            };
            spellTableNode.Children.Add(spellSetsNode);
            
            portalRoot.Children.Add(spellTableNode);
            
            // --- NEW: Add CharGen with children ---
            var charGenNode = new TreeViewNode()
            {
                Content = new TreeNodeData
                {
                    DisplayName = "CharGen",
                    Identifier = new NodeIdentifier { FileId = DatFileIds.CharGenId, DatabaseId = dbId } // Main node identifier
                },
                IsExpanded = false
            };

            var startingAreasNode = new TreeViewNode()
            {
                Content = new TreeNodeData
                {
                    DisplayName = "Starting Areas",
                    Identifier = new NodeIdentifier { FileId = DatFileIds.CharGenId, DatabaseId = dbId, Subtype = "StartingAreas" }
                }
            };
            charGenNode.Children.Add(startingAreasNode);

            var heritageGroupsNode = new TreeViewNode()
            {
                Content = new TreeNodeData
                {
                    DisplayName = "Heritage Groups",
                    Identifier = new NodeIdentifier { FileId = DatFileIds.CharGenId, DatabaseId = dbId, Subtype = "HeritageGroups" }
                }
            };
            charGenNode.Children.Add(heritageGroupsNode);
            portalRoot.Children.Add(charGenNode);
            // --- END NEW SECTION ---
            
            // --- NEW: Add ChatPoseTable with children ---
            var chatPoseTableNode = new TreeViewNode()
            {
                Content = new TreeNodeData
                {
                    DisplayName = "ChatPoseTable",
                    Identifier = new NodeIdentifier { FileId = DatFileIds.ChatPoseTableId, DatabaseId = dbId } // Main node identifier
                },
                IsExpanded = false
            };

            var chatPosesNode = new TreeViewNode()
            {
                Content = new TreeNodeData
                {
                    DisplayName = "Chat Poses",
                    Identifier = new NodeIdentifier { FileId = DatFileIds.ChatPoseTableId, DatabaseId = dbId, Subtype = "ChatPoses" }
                }
            };
            chatPoseTableNode.Children.Add(chatPosesNode);

            var chatEmotesNode = new TreeViewNode()
            {
                Content = new TreeNodeData
                {
                    DisplayName = "Chat Emotes",
                    Identifier = new NodeIdentifier { FileId = DatFileIds.ChatPoseTableId, DatabaseId = dbId, Subtype = "ChatEmotes" }
                }
            };
            chatPoseTableNode.Children.Add(chatEmotesNode);
            portalRoot.Children.Add(chatPoseTableNode);
            // --- END NEW SECTION ---
            
            // Add all the standard nodes (CharGen and ChatPoseTable removed from here)
            TryAddNode(portalRoot, "Animation", DatFileIds.AnimationTableId, dbId);
            TryAddNode(portalRoot, "AnimationHooks", DatFileIds.AnimationHookTableId, dbId);
            TryAddNode(portalRoot, "ChatEmotes", DatFileIds.ChatEmoteTableId, dbId);
            TryAddNode(portalRoot, "Clothing", DatFileIds.ClothingTableId, dbId);
            TryAddNode(portalRoot, "CombatTable", DatFileIds.CombatTableId, dbId);
            TryAddNode(portalRoot, "Dungeons", DatFileIds.DungeonTableId, dbId);
            TryAddNode(portalRoot, "ExperienceTable", DatFileIds.ExperienceTableId, dbId);
            TryAddNode(portalRoot, "FileMap", DatFileIds.FileMapId, dbId);
            TryAddNode(portalRoot, "GameEventTable", DatFileIds.GameEventTableId, dbId);
            TryAddNode(portalRoot, "Generators", DatFileIds.GeneratorTableId, dbId);
            TryAddNode(portalRoot, "GfxObjs", DatFileIds.GfxObjTableId, dbId);
            TryAddNode(portalRoot, "LanguageStrings", DatFileIds.LanguageTableId, dbId);
            TryAddNode(portalRoot, "Materials", DatFileIds.MaterialTableId, dbId);
            TryAddNode(portalRoot, "MotionTables", DatFileIds.MotionTableId, dbId);
            TryAddNode(portalRoot, "Palette", DatFileIds.PaletteTableId, dbId);
            TryAddNode(portalRoot, "ParticleEmitters", DatFileIds.ParticleEmitterTableId, dbId);
            TryAddNode(portalRoot, "QualityFilters", DatFileIds.QualityFilterTableId, dbId);
            TryAddNode(portalRoot, "RenderMaterials", DatFileIds.RenderMaterialTableId, dbId);
            TryAddNode(portalRoot, "SkillTable", DatFileIds.SkillTableId, dbId);
            TryAddNode(portalRoot, "Sounds", DatFileIds.SoundTableId, dbId);
            TryAddNode(portalRoot, "SpellComponentsTable", DatFileIds.SpellComponentsTableId, dbId);
            TryAddNode(portalRoot, "StringStateTable", DatFileIds.StringStateTableId, dbId);
            TryAddNode(portalRoot, "Surfaces", DatFileIds.SurfaceTableId, dbId);
            TryAddNode(portalRoot, "Textures", DatFileIds.TextureTableId, dbId);
            TryAddNode(portalRoot, "UILayouts", DatFileIds.UILayoutTableId, dbId);
            TryAddNode(portalRoot, "VitalTable", DatFileIds.VitalTableId, dbId);
            TryAddNode(portalRoot, "WeenieDefaults", DatFileIds.WeenieDefaultsId, dbId);
            TryAddNode(portalRoot, "BadDataTable", DatFileIds.BadDataTableId, dbId);
            
            // Now dynamically find and add all public IEnumerable properties that might be collections
            foreach (var prop in typeof(PortalDatabase).GetProperties())
            {
                // Skip properties we've already added manually
                if (IsPredefinedProperty(prop.Name)) continue;
                
                // Only look at readable properties that might be collections
                if (prop.CanRead && typeof(IEnumerable).IsAssignableFrom(prop.PropertyType) && prop.PropertyType != typeof(string))
                {
                    var value = prop.GetValue(portalDb);
                    if (value != null)
                    {
                        TryAddNodeForCollection(portalRoot, prop.Name, $"{prop.Name}_{dbId}", value as IEnumerable);
                        Debug.WriteLine($"Dynamically added property: {prop.Name}");
                    }
                }
            }

            Debug.WriteLine($"Portal Root Node has {portalRoot.Children.Count} children before adding to TreeView.");
            _treeView.RootNodes.Add(portalRoot); // Add the fully constructed root node
            Debug.WriteLine($"Added portalRoot to TreeView.RootNodes. Count: {_treeView.RootNodes.Count}");
            
            // Handle selection event to make sure items select correctly
            _treeView.ItemInvoked += TreeView_ItemInvoked;
        }
        
        /// <summary>
        /// Populate the TreeView with Cell database content
        /// </summary>
        /// <param name="cellDb">The Cell database</param>
        /// <param name="fileName">The filename of the database</param>
        /// <param name="dbId">The database ID</param>
        public void PopulateCellTreeView(CellDatabase cellDb, string fileName, string dbId)
        {
            Debug.WriteLine("Populating Cell TreeView...");

            var cellRoot = new TreeViewNode()
            {
                Content = new TreeNodeData 
                { 
                    DisplayName = $"Cell Data - {fileName}", 
                    Identifier = $"Root_Cell_{dbId}" 
                },
                IsExpanded = true // Ensure root is expanded
            };
            
            Debug.WriteLine($"Created Root Node: {((TreeNodeData)cellRoot.Content).DisplayName}");

            // Add the known collection properties
            TryAddNodeForCollection(cellRoot, "EnvCells", $"{DatFileIds.EnvCellsCollectionTag}_{dbId}", cellDb.EnvCells);
            TryAddNodeForCollection(cellRoot, "LandBlockInfos", $"{DatFileIds.LandBlockInfosCollectionTag}_{dbId}", cellDb.LandBlockInfos);
            TryAddNodeForCollection(cellRoot, "LandBlocks", $"{DatFileIds.LandBlocksCollectionTag}_{dbId}", cellDb.LandBlocks);

            // Now dynamically find and add all public IEnumerable properties that might be collections
            foreach (var prop in typeof(CellDatabase).GetProperties())
            {
                // Skip properties we've already added manually
                if (prop.Name == "EnvCells" || prop.Name == "LandBlockInfos" || prop.Name == "LandBlocks") continue;
                
                // Only look at readable properties that might be collections
                if (prop.CanRead && typeof(IEnumerable).IsAssignableFrom(prop.PropertyType) && prop.PropertyType != typeof(string))
                {
                    var value = prop.GetValue(cellDb);
                    if (value != null)
                    {
                        TryAddNodeForCollection(cellRoot, prop.Name, $"{prop.Name}_{dbId}", value as IEnumerable);
                        Debug.WriteLine($"Dynamically added property: {prop.Name}");
                    }
                }
            }

            Debug.WriteLine($"Cell Root Node has {cellRoot.Children.Count} children before adding to TreeView.");
            _treeView.RootNodes.Add(cellRoot); // Add the fully constructed root node
            Debug.WriteLine($"Added cellRoot to TreeView.RootNodes. Count: {_treeView.RootNodes.Count}");
        }
        
        /// <summary>
        /// Clear all nodes from the TreeView
        /// </summary>
        public void ClearTreeView()
        {
            _treeView.RootNodes.Clear();
        }
        
        /// <summary>
        /// Helper method to add a node for a specific File ID (single-file table or range start)
        /// </summary>
        private void TryAddNode(TreeViewNode parent, string displayName, uint identifier, string dbId)
        {
            var childNode = new TreeViewNode()
            {
                Content = new TreeNodeData 
                { 
                    DisplayName = displayName, 
                    Identifier = new NodeIdentifier { FileId = identifier, DatabaseId = dbId }
                }
            };
            parent.Children.Add(childNode);
            Debug.WriteLine($"  Added child node: {displayName}");
        }

        /// <summary>
        /// Helper method to add a node if the corresponding collection is not null or empty
        /// </summary>
        private void TryAddNodeForCollection(TreeViewNode parent, string displayName, string identifierTag, IEnumerable? collection)
        {
            bool hasItems = collection switch
            {
                ICollection iColl => iColl.Count > 0,
                IEnumerable iEnum => iEnum.Cast<object>().Any(),
                _ => collection != null
            };

            if (hasItems)
            {
                var childNode = new TreeViewNode()
                {
                     Content = new TreeNodeData { DisplayName = displayName, Identifier = identifierTag } // Use string tag as identifier
                };
                parent.Children.Add(childNode);
                 Debug.WriteLine($"  Added child node (collection): {displayName}");
            }
            else
            {
                 Debug.WriteLine($"  Skipped child node (collection empty/null): {displayName}");
            }
        }
        
        /// <summary>
        /// Helper method to check if a property is already predefined in our manual adds
        /// </summary>
        private bool IsPredefinedProperty(string propertyName)
        {
            // List of property names we're adding manually to avoid duplicates
            string[] predefinedProps = new[] {
                "SpellTable", 
                "CharGen", 
                "Animation", "AnimationHooks", "ChatEmotes", "Clothing", 
                "CombatTable", "Dungeons", "ExperienceTable", "FileMap", "GameEventTable", 
                "Generators", "GfxObjs", "LanguageStrings", "Materials", "MotionTables", 
                "Palette", "ParticleEmitters", "QualityFilters", "RenderMaterials", "SkillTable", 
                "Sounds", "SpellComponentsTable", "StringStateTable", "Surfaces", 
                "Textures", "UILayouts", "VitalTable", "WeenieDefaults"
            };
            
            return predefinedProps.Any(p => p.Equals(propertyName, StringComparison.OrdinalIgnoreCase));
        }
        
        /// <summary>
        /// Handle item invoked to ensure correct selection
        /// </summary>
        private void TreeView_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs e)
        {
            // When a TreeView item is invoked, ensure the Content is properly set as the SelectedItem
            if (e.InvokedItem is TreeViewNode node && node.Content is TreeNodeData nodeData)
            {
                // Update the selection directly
                sender.SelectedItem = nodeData;
                Debug.WriteLine($"Updated selection to TreeNodeData: {nodeData.DisplayName}");
            }
        }
    }
} 