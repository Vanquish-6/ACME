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
            
            // Add nodes based on corrected DatFileIds constants
            TryAddNode(portalRoot, "Weenie Defaults", DatFileIds.WeenieDefaultsId, dbId);
            TryAddNode(portalRoot, "Skill Table", DatFileIds.SkillTableId, dbId);
            TryAddNode(portalRoot, "Spell Components", DatFileIds.SpellComponentsTableId, dbId);
            TryAddNode(portalRoot, "Experience Table", DatFileIds.ExperienceTableId, dbId);
            TryAddNode(portalRoot, "Taboo Table", DatFileIds.TabooTableId, dbId);
            TryAddNode(portalRoot, "Game Event Table", DatFileIds.GameEventTableId, dbId);

            TryAddNode(portalRoot, "GfxObj", DatFileIds.GfxObjId, dbId);
            TryAddNode(portalRoot, "Setups", DatFileIds.SetupId, dbId);
            TryAddNode(portalRoot, "Animations", DatFileIds.AnimationId, dbId);
            TryAddNode(portalRoot, "Animation Hook Ops", DatFileIds.AnimationHookOpId, dbId);
            TryAddNode(portalRoot, "Surface Textures", DatFileIds.SurfaceTextureId, dbId);
            TryAddNode(portalRoot, "Textures", DatFileIds.RenderSurfaceId, dbId);
            TryAddNode(portalRoot, "Surfaces", DatFileIds.SurfaceId, dbId);
            TryAddNode(portalRoot, "Motion Tables", DatFileIds.MotionTableId, dbId);
            TryAddNode(portalRoot, "Sound Tables", DatFileIds.SoundTableId, dbId);
            TryAddNode(portalRoot, "Sound Resources", DatFileIds.SoundResourceId, dbId);
            TryAddNode(portalRoot, "Palettes", DatFileIds.PaletteId, dbId);
            TryAddNode(portalRoot, "Palette Sets", DatFileIds.PaletteSetId, dbId);
            TryAddNode(portalRoot, "Clothing", DatFileIds.ClothingTableId, dbId);
            TryAddNode(portalRoot, "Regions", DatFileIds.RegionId, dbId);
            TryAddNode(portalRoot, "Keymaps", DatFileIds.KeymapId, dbId);
            TryAddNode(portalRoot, "Render Textures", DatFileIds.RenderTextureId, dbId);
            TryAddNode(portalRoot, "Quality Filters", DatFileIds.QualityFilterId, dbId);
            TryAddNode(portalRoot, "Render Materials", DatFileIds.RenderMaterialId, dbId);
            TryAddNode(portalRoot, "Fonts", DatFileIds.FontId, dbId);
            TryAddNode(portalRoot, "Materials", DatFileIds.MaterialId, dbId);
            TryAddNode(portalRoot, "Physics Script Tables", DatFileIds.PhysicsScriptTableId, dbId);
            TryAddNode(portalRoot, "Particle Emitters", DatFileIds.ParticleEmitterTableId, dbId);
            TryAddNode(portalRoot, "Generator Profiles", DatFileIds.GeneratorProfileId, dbId);
            TryAddNode(portalRoot, "Language Strings", DatFileIds.LanguageStringDataId, dbId);
            TryAddNode(portalRoot, "Environments", DatFileIds.EnvironmentId, dbId);

            // Note: Local Dat types (UILayout, StringTable, FontLocal, StringState) are not added here
            // as this function populates for PortalDatabase.
            
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
            // List of property names from PortalDatabase that are handled manually via TryAddNode
            // This prevents the dynamic loop from adding them again.
            // Note: Names must EXACTLY match the PortalDatabase property names (case-sensitive).
            string[] predefinedProps = new[] {
                // Single-file object properties (won't be picked up by IEnumerable loop, but kept for clarity)
                "CharGen", 
                "ChatPoseTable", 
                "ExperienceTable", 
                "GameEventTable", 
                "SkillTable", 
                "SpellComponentTable", 
                "SpellTable", // Handles Spells/SpellSets sub-nodes manually
                "TabooTable", 
                "WeenieDefaults",
                // Collection properties handled by TryAddNode
                "Animations",
                "Clothings",
                "GfxObjs",
                "Keymaps",
                "LanguageStrings",
                "MotionTables",
                "Palettes",
                "PaletteSets",
                "ParticleEmitters",
                "PhysicsScripts",
                "Regions",
                "RenderSurfaces",
                "Scenes",
                "Setups",
                "Surfaces",
                "SurfaceTextures",
                "Environments",
                // Property names NOT listed here (e.g., Waves, MaterialModifiers, etc.) 
                // will be added dynamically by the loop, which is intended.
            };
            
            // Use Ordinal comparison for exact, case-sensitive matching
            return predefinedProps.Any(p => p.Equals(propertyName, StringComparison.Ordinal)); 
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