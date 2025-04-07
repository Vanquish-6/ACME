using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using DatReaderWriter;
using DatReaderWriter.DBObjs;
using DatReaderWriter.Enums;
using DatReaderWriter.Types;
using DatReaderWriter.Lib.IO.DatBTree;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using ACME.Constants;
using ACME.Models;
using ACME.Renderers;
using Windows.UI.Text;
using ACME.Extractors;
using ACME.Helpers;
using System.Dynamic;

namespace ACME.Managers
{
    /// <summary>
    /// Event args for indicating relevance of the current data view for specific controls.
    /// </summary>
    public class RelevantDataViewChangedEventArgs : EventArgs
    {
        public bool IsSpellViewRelevant { get; } = false;
        // Add flags for other relevant views if needed later

        public RelevantDataViewChangedEventArgs(bool isSpellViewRelevant)
        {
            IsSpellViewRelevant = isSpellViewRelevant;
        }
    }

    /// <summary>
    /// Manager class for handling data loading for TreeView selections
    /// </summary>
    public class TreeViewDataLoader
    {
        private readonly DatabaseManager _databaseManager;
        private readonly ListView _itemListView;
        private readonly DetailRenderer _detailRenderer;
        private SpellTable? _currentSpellTable; // Store the currently loaded spell table for filtering
        
        /// <summary>
        /// Maintains a reference to the last successful node data for selection handling
        /// </summary>
        private TreeNodeData? _lastSuccessfulNodeData = null;
        
        /// <summary>
        /// Event raised when data loading starts
        /// </summary>
        public event EventHandler? DataLoadingStarted;
        
        /// <summary>
        /// Event raised when data loading completes
        /// </summary>
        public event EventHandler? DataLoadingCompleted;
        
        /// <summary>
        /// Event raised when the data view changes relevance for specific UI controls (e.g., spell filter).
        /// </summary>
        public event EventHandler<RelevantDataViewChangedEventArgs>? RelevantDataViewChanged;
        
        public TreeViewDataLoader(DatabaseManager databaseManager, ListView itemListView, DetailRenderer detailRenderer)
        {
            _databaseManager = databaseManager ?? throw new ArgumentNullException(nameof(databaseManager));
            _itemListView = itemListView ?? throw new ArgumentNullException(nameof(itemListView));
            _detailRenderer = detailRenderer ?? throw new ArgumentNullException(nameof(detailRenderer));
            
            // Wire up generic ListView selection changed event
            _itemListView.SelectionChanged += ItemListView_SelectionChanged;
        }
        
        /// <summary>
        /// Process the selected TreeView item and load appropriate data
        /// (Restored and adapted from MainWindow.xaml.cs)
        /// </summary>
        /// <param name="selectedNode">The newly selected TreeViewNode</param>
        /// <returns>Task representing the async operation</returns>
        public async Task ProcessTreeViewSelectionAsync(TreeViewNode? selectedNode)
        {
            DataLoadingStarted?.Invoke(this, EventArgs.Empty);
            _detailRenderer.ClearAndSetMessage("Processing selection...", isError: false);

            LogLoadedDatabases();

            _itemListView.ItemsSource = null;
            _detailRenderer.ClearAndAddDefaultTitle();

            Debug.WriteLine("==== TreeView Selection Changed (DataLoader) ====");

            if (_databaseManager.LoadedDatabases.Count == 0)
            {
                Debug.WriteLine("No databases loaded");
                _detailRenderer.ClearAndSetMessage("ERROR: No databases are loaded", isError: true);
                GoToCompletionState();
                return;
            }

            TreeNodeData? nodeData = ExtractNodeDataFromNode(selectedNode);

            if (nodeData == null || nodeData.Identifier == null)
            {
                Debug.WriteLine("SelectionChanged: Failed to resolve TreeNodeData.");
                 _detailRenderer.ClearAndSetMessage($"ERROR: Could not resolve tree node data.", isError: true);
                GoToCompletionState();
                return;
            }

            _lastSuccessfulNodeData = nodeData;
            Debug.WriteLine($"SelectionChanged: Node '{nodeData.DisplayName}', Identifier '{nodeData.Identifier}'");
            _detailRenderer.ClearAndSetMessage($"Node: {nodeData.DisplayName}, Identifier: {nodeData.Identifier}", isError: false);

            object? itemsSource = null;
            bool displayDirectly = false;

            try
            {
                string? dbId = GetDatabaseIdFromIdentifier(nodeData.Identifier);
                if (string.IsNullOrEmpty(dbId))
                {
                    Debug.WriteLine("Database ID is empty or could not be extracted.");
                    _detailRenderer.ClearAndSetMessage($"Could not determine database for '{nodeData.DisplayName}'.", isError: true);
                    GoToCompletionState();
                    return;
                }

                var targetDbInfo = _databaseManager.FindDatabase(dbId);
                Debug.WriteLine($"Looking for database with ID: {dbId}");
                Debug.WriteLine($"Found database: {(targetDbInfo != null ? targetDbInfo.FileName : "null")}");
                 _detailRenderer.ClearAndSetMessage($"Database: {(targetDbInfo != null ? targetDbInfo.FileName : "Not found")}", isError: false);

                if (targetDbInfo == null)
                {
                    Debug.WriteLine($"Database not found for ID {dbId}");
                    _detailRenderer.ClearAndSetMessage($"ERROR: Database not found for '{nodeData.DisplayName}'", isError: true);
                    GoToCompletionState();
                    return;
                }

                var targetDb = targetDbInfo.Database;

                if (nodeData.Identifier is NodeIdentifier nodeId)
                {
                    itemsSource = LoadDataForNodeIdentifier(nodeId, targetDb, nodeData.DisplayName, out displayDirectly);
                }
                else if (nodeData.Identifier is string strTag)
                {
                    itemsSource = LoadDataForStringTag(strTag, targetDb, dbId, nodeData.DisplayName, out displayDirectly);
                }

                UpdateUIWithLoadedData(itemsSource, displayDirectly, nodeData.DisplayName);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"!!!!! EXCEPTION during data loading/binding: {ex.GetType().Name} - {ex.Message}");
                Debug.WriteLine(ex.StackTrace);
                _detailRenderer.ClearAndSetMessage($"Error loading data: {ex.Message}", isError: true);
                _itemListView.ItemsSource = null; // Clear list on error
            }
            finally
            {
                GoToCompletionState();
            }
        }
        
        /// <summary>
        /// Loads data based on a NodeIdentifier.
        /// </summary>
        private object? LoadDataForNodeIdentifier(NodeIdentifier nodeId, DatDatabase targetDb, string displayName, out bool displayDirectly)
        {
            displayDirectly = false;
            uint fileId = nodeId.FileId;
            string subtype = nodeId.Subtype;
            object? itemsSource = null;
            Debug.WriteLine($"Processing node with FileId: 0x{fileId:X8}, Subtype: {subtype}");

            // Clear the current spell table reference when loading new data
            _currentSpellTable = null; 
            // Signal that the spell view is initially not relevant
            RelevantDataViewChanged?.Invoke(this, new RelevantDataViewChangedEventArgs(false));

            if (targetDb is PortalDatabase portalDb)
            {
                if (fileId == DatFileIds.SpellTableId)
                {
                    Debug.WriteLine($"Attempting to load SpellTable with fileId: 0x{fileId:X8}");
                    _detailRenderer.ClearAndSetMessage($"Attempting to load SpellTable...", isError: false);
                    if (portalDb.TryReadFile<SpellTable>(fileId, out var spellTable) && spellTable != null)
                    {
                        Debug.WriteLine($"Successfully read SpellTable: {(spellTable != null ? "non-null" : "null")}");
                        // Ensure Microsoft.UI.Colors is used
                        _detailRenderer.ClearAndSetMessage($"Successfully read SpellTable file", isError: false);
                        if (string.IsNullOrEmpty(subtype) || subtype == "Spells")
                        {
                            // --- START FILTERING LOGIC (Now uses helper method) ---
                             _currentSpellTable = spellTable; // <-- Store the reference
                            _detailRenderer.ClearAndAddDefaultTitle();
                            // Show total before filtering message
                             _detailRenderer.AddInfoMessage($"Total Spells (Unfiltered): {spellTable.Spells?.Count ?? 0}");

                             // Call the helper method with initial empty filters
                            itemsSource = GetFilteredSpellItemsSource(spellTable, ""); 
                            // Signal that the spell view IS now relevant
                            RelevantDataViewChanged?.Invoke(this, new RelevantDataViewChangedEventArgs(true));
                            // --- END FILTERING LOGIC (Now uses helper method) ---
                        }
                        else if (subtype == "SpellSets")
                        {
                            _detailRenderer.ClearAndAddDefaultTitle();
                             _detailRenderer.AddInfoMessage($"Total Spell Sets: {spellTable.SpellsSets?.Count ?? 0}");
                             if (spellTable.SpellsSets != null && spellTable.SpellsSets.Count > 0)
                             {
                                 itemsSource = spellTable.SpellsSets.Select(s => new { Id = s.Key, Count = s.Value?.SpellSetTiers?.Count ?? 0, DisplayText = $"{s.Key}: Set ({s.Value?.SpellSetTiers?.Count ?? 0} tiers)", Value = s.Value }).OrderBy(s => s.Id).ToList();
                                 _detailRenderer.ClearAndSetMessage("Select a spell set from the list.", isError: false);
                             }
                             else { _detailRenderer.AddInfoMessage("No spell sets found."); }
                        }
                        else { _detailRenderer.ClearAndSetMessage($"Unknown SpellTable subtype: {subtype}", isError: true); }
                    }
                    else { _detailRenderer.ClearAndSetMessage("Failed to load SpellTable data.", isError: true); }
                }
                else if (fileId == DatFileIds.SkillTableId)
                {
                    if (portalDb.TryReadFile<SkillTable>(fileId, out var skillTable) && skillTable != null)
                    {
                        _detailRenderer.ClearAndAddDefaultTitle();
                        _detailRenderer.AddInfoMessage($"Total Skills: {skillTable.Skills?.Count ?? 0}");
                        if (skillTable.Skills != null && skillTable.Skills.Count > 0)
                        {
                             itemsSource = skillTable.Skills.Select(s => new { Id = s.Key, Name = s.Value?.Name ?? "?", DisplayText = $"{(int)s.Key}: {s.Value?.Name ?? "?"}", Value = s.Value }).OrderBy(s => s.Id).ToList();
                             _detailRenderer.ClearAndSetMessage("Select a skill from the list.", isError: false);
                        }
                        else { _detailRenderer.AddInfoMessage("No skills found."); }
                    }
                     else { _detailRenderer.ClearAndSetMessage("Failed to load SkillTable data.", isError: true); }
                }
                 else if (fileId == DatFileIds.SpellComponentsTableId)
                 {
                     if (portalDb.TryReadFile<SpellComponentTable>(fileId, out var spellCompTable) && spellCompTable != null)
                     {
                         _detailRenderer.ClearAndAddDefaultTitle();
                         _detailRenderer.AddInfoMessage($"Total Components: {spellCompTable.Components?.Count ?? 0}");
                         if (spellCompTable.Components != null && spellCompTable.Components.Count > 0)
                         {
                             itemsSource = spellCompTable.Components.Select(c => new { Id = c.Key, Name = c.Value?.Name ?? "?", DisplayText = $"{c.Key}: {c.Value?.Name ?? "?"}", Value = c.Value }).OrderBy(c => c.Id).ToList();
                             _detailRenderer.ClearAndSetMessage("Select a component from the list.", isError: false);
                         }
                         else { _detailRenderer.AddInfoMessage("No components found."); }
                     }
                      else { _detailRenderer.ClearAndSetMessage("Failed to load SpellComponentTable data.", isError: true); }
                 }
                // --- NEW: Handle ChatPoseTable based on Subtype --- 
                else if (fileId == DatFileIds.ChatPoseTableId)
                {
                    if (string.IsNullOrEmpty(subtype))
                    {
                        // User clicked the main "ChatPoseTable" node
                        _detailRenderer.ClearAndAddDefaultTitle();
                        _detailRenderer.AddInfoMessage("Select 'Chat Poses' or 'Chat Emotes' from the tree.");
                        itemsSource = null;
                        displayDirectly = false;
                    }
                    else if (portalDb.TryReadFile<ChatPoseTable>(fileId, out var chatPoseTable) && chatPoseTable != null)
                    {
                        if (subtype == "ChatPoses")
                        {
                            _detailRenderer.ClearAndAddDefaultTitle();
                            _detailRenderer.AddInfoMessage($"Listing Chat Poses ({chatPoseTable.ChatPoseHash?.Count ?? 0} entries). Select one to view details.");
                            // Transform dictionary into list for ListView
                            itemsSource = chatPoseTable.ChatPoseHash?
                                .Select(kvp => new { DisplayText = kvp.Key, Value = kvp.Value }) // Use Key for display
                                .OrderBy(item => item.DisplayText)
                                .ToList<object>(); 
                            displayDirectly = false; // List the dictionary entries first
                        }
                        else if (subtype == "ChatEmotes")
                        {
                            _detailRenderer.ClearAndAddDefaultTitle();
                             _detailRenderer.AddInfoMessage($"Listing Chat Emotes ({chatPoseTable.ChatEmoteHash?.Count ?? 0} entries). Select one to view details.");
                             // Transform dictionary into list for ListView
                             itemsSource = chatPoseTable.ChatEmoteHash?
                                .Select(kvp => new { DisplayText = kvp.Key, Value = kvp.Value }) // Use Key for display
                                .OrderBy(item => item.DisplayText)
                                .ToList<object>(); 
                            displayDirectly = false; // List the dictionary entries first
                        }
                        else
                        {
                            _detailRenderer.ClearAndSetMessage($"Unknown ChatPoseTable subtype: {subtype}", isError: true);
                            itemsSource = null;
                            displayDirectly = false;
                        }
                    }
                    else
                    {
                        _detailRenderer.ClearAndSetMessage("Failed to load ChatPoseTable data.", isError: true);
                    }
                }
                // --- Single Object Tables ---
                 else if (fileId == DatFileIds.ExperienceTableId && portalDb.TryReadFile<ExperienceTable>(fileId, out var expTable))
                 {
                     displayDirectly = false;
                     itemsSource = new List<object>
                     {
                         new { DisplayText = "Attribute XP",      Data = expTable.Attributes },
                         new { DisplayText = "Vital XP",          Data = expTable.Vitals },
                         new { DisplayText = "Trained Skill XP",  Data = expTable.TrainedSkills },
                         new { DisplayText = "Specialized Skill XP",Data = expTable.SpecializedSkills },
                         new { DisplayText = "Character Level XP",Data = expTable.Levels },
                         new { DisplayText = "Skill Credits",     Data = expTable.SkillCredits }
                     };
                     _detailRenderer.ClearAndAddDefaultTitle();
                     _detailRenderer.AddInfoMessage("Select an XP category from the list.");
                 }
                 else if (fileId == DatFileIds.VitalTableId)
                 {
                     if (portalDb.TryReadFile<VitalTable>(fileId, out var vitalTable) && vitalTable != null)
                     {
                         _detailRenderer.ClearAndAddDefaultTitle();
                         _detailRenderer.AddInfoMessage("Select a vital (Health, Stamina, or Mana) to view details.");
                         itemsSource = new List<object>
                         {
                             new { DisplayText = "Health", Value = vitalTable.Health },
                             new { DisplayText = "Stamina", Value = vitalTable.Stamina },
                             new { DisplayText = "Mana", Value = vitalTable.Mana }
                         };
                         displayDirectly = false; // Display the list first
                     }
                     else
                     {
                         _detailRenderer.ClearAndSetMessage("Failed to load VitalTable data.", isError: true);
                         itemsSource = null;
                         displayDirectly = false;
                     }
                 }
                 else if (fileId == DatFileIds.BadDataTableId)
                 {
                     if (portalDb.TryReadFile<BadDataTable>(fileId, out var badDataTable) && badDataTable?.BadIds != null)
                     {
                         displayDirectly = false;
                         itemsSource = badDataTable.BadIds
                             .Select(kvp => new { DisplayText = $"Key: 0x{kvp.Key:X8}, Value: 0x{kvp.Value:X8}", Value = kvp })
                             .OrderBy(item => item.Value.Key) // Order by key for consistency
                             .ToList();
                         _detailRenderer.ClearAndAddDefaultTitle();
                         _detailRenderer.AddInfoMessage($"Found {badDataTable.BadIds.Count} bad ID entries. Select one from the list.");
                     }
                     else
                     {
                         _detailRenderer.ClearAndSetMessage("Failed to load BadDataTable data or it contains no entries.", isError: true);
                         itemsSource = null;
                         displayDirectly = false;
                     }
                 }
                // --- CharGen with Subtypes ---
                 else if (fileId == DatFileIds.CharGenId)
                 {
                     if (portalDb.TryReadFile<CharGen>(fileId, out var charGen) && charGen != null)
                     {
                         _detailRenderer.ClearAndSetMessage("Successfully read CharGen file", isError: false);
                         if (subtype == "StartingAreas")
                         {
                             _detailRenderer.ClearAndAddDefaultTitle();
                             if (charGen.StartingAreas != null && charGen.StartingAreas.Count > 0)
                             {
                                 itemsSource = charGen.StartingAreas.Select((sa, index) => new { Index = index, Name = sa.Name ?? "?", DisplayText = $"[{index}] {sa.Name ?? "?"}", Value = sa }).ToList();
                                 _detailRenderer.ClearAndSetMessage($"Select a starting area from the list ({charGen.StartingAreas.Count} found).", isError: false);
                             }
                             else { _detailRenderer.ClearAndSetMessage("No starting areas found.", isError: false); }
                             displayDirectly = false;
                         }
                         else if (subtype == "HeritageGroups")
                         {
                             _detailRenderer.ClearAndAddDefaultTitle();
                             if (charGen.HeritageGroups != null && charGen.HeritageGroups.Count > 0)
                             {
                                 itemsSource = charGen.HeritageGroups.Select(kvp => new { Id = kvp.Key, Name = kvp.Value?.Name ?? "?", DisplayText = $"{kvp.Key}: {kvp.Value?.Name ?? "?"}", Value = kvp.Value }).OrderBy(hg => hg.Id).ToList();
                                 _detailRenderer.ClearAndSetMessage($"Select a heritage group from the list ({charGen.HeritageGroups.Count} found).", isError: false);
                             }
                             else { _detailRenderer.ClearAndSetMessage("No heritage groups found.", isError: false); }
                             displayDirectly = false;
                         }
                         else // Base CharGen node selected
                         {
                              _detailRenderer.ClearAndAddDefaultTitle();
                              _detailRenderer.AddInfoMessage("Select 'Starting Areas' or 'Heritage Groups' from the tree.");
                              itemsSource = null;
                              displayDirectly = false; // Don't display the raw CharGen object
                         }
                     }
                     else { _detailRenderer.ClearAndSetMessage("Failed to load CharGen data.", isError: true); }
                 }
                 // --- File Range Tables (Restored LanguageTableId to IsRangeTableId) ---
                 else if (IsRangeTableId(fileId))
                 {
                     displayDirectly = false; // Ensure range tables are listed first
                     try
                     {
                         var items = targetDb.Tree.GetFilesInRange(fileId, fileId | 0x00FFFFFF);
                         itemsSource = items?.Select(f => new { Id = f.Id, DisplayText = $"File 0x{f.Id:X8}", Value = f }).ToList();
                         _detailRenderer.ClearAndAddDefaultTitle();
                         // Cast to IList to safely get the Count for any list type
                         int count = (itemsSource as System.Collections.IList)?.Count ?? 0;
                         _detailRenderer.AddInfoMessage($"Found {count} files. Select one from the list.");
                     }
                     catch (Exception rangeEx)
                     { Debug.WriteLine($"Error getting files in range for {displayName}: {rangeEx.Message}"); _detailRenderer.ClearAndSetMessage($"Error listing files for {displayName}.", isError: true); }
                 }
                else { _detailRenderer.ClearAndSetMessage($"Loading not implemented for '{displayName}' (0x{fileId:X8}).", isError: true); }

                 // Add error messages if TryReadFile failed for single objects
                 if (displayDirectly && itemsSource == null)
                 { _detailRenderer.ClearAndSetMessage($"Failed to load data for {displayName}.", isError: true); }
            }
            else { _detailRenderer.ClearAndSetMessage($"Loading not implemented for '{displayName}' in non-Portal DB.", isError: true); }
            return itemsSource;
        }
        
        /// <summary>
        /// Applies the current filters to the spell list view.
        /// </summary>
        public void ApplySpellFilter(string nameFilter /* TODO: Add other filter parameters */)
        {
            if (_currentSpellTable != null)
            {
                Debug.WriteLine($"ApplySpellFilter called with Name: '{nameFilter}'");
                // Use the helper method to get the filtered source
                object? filteredSource = GetFilteredSpellItemsSource(_currentSpellTable, nameFilter);
                // Update the ListView's ItemsSource directly
                _itemListView.ItemsSource = filteredSource;
                // Ensure relevance is still true when applying filter successfully
                 RelevantDataViewChanged?.Invoke(this, new RelevantDataViewChangedEventArgs(true));
            }
            else
            {
                Debug.WriteLine("ApplySpellFilter called, but no SpellTable is currently loaded/selected.");
                // Signal that the spell view is not relevant if filter is called inappropriately
                 RelevantDataViewChanged?.Invoke(this, new RelevantDataViewChangedEventArgs(false));
                // Optionally clear the list or show a message if the spell node isn't active
                // _itemListView.ItemsSource = null;
                // _detailRenderer.AddInfoMessage("Select the 'Spells' node to enable filtering.");
            }
        }
        
        /// <summary>
        /// Applies filters to a SpellTable and returns the ItemsSource for the ListView.
        /// Also updates the DetailRenderer messages.
        /// </summary>
        private object? GetFilteredSpellItemsSource(SpellTable spellTable, string nameFilter /* TODO: Add other filters as parameters */)
        {
            object? itemsSource = null;
            MagicSchool? schoolFilter = null; // Placeholder for future filter
            uint? componentFilter = null; // Placeholder for future filter

            if (spellTable.Spells != null && spellTable.Spells.Count > 0)
            {
                var filteredSpells = spellTable.Spells.Values.AsEnumerable(); // Start query

                if (!string.IsNullOrWhiteSpace(nameFilter))
                {
                    filteredSpells = filteredSpells.Where(s => s.Name != null && s.Name.Contains(nameFilter, StringComparison.OrdinalIgnoreCase));
                }
                if (schoolFilter.HasValue) // Keep placeholder logic
                {
                    filteredSpells = filteredSpells.Where(s => s.School == schoolFilter.Value);
                }
                if (componentFilter.HasValue) // Keep placeholder logic
                {
                    // Ensure Components list is not null before checking
                    filteredSpells = filteredSpells.Where(s => s.Components != null && s.Components.Contains(componentFilter.Value));
                }

                // Convert filtered results to list for the UI
                // Need the original dictionary to get the Key (ID) back efficiently
                var filteredList = filteredSpells
                    .Select(s => 
                    {
                        // Find the key corresponding to this spell value
                        // This isn't super efficient, but necessary if we need the ID display
                        // An alternative is to pass the spell object directly and let the renderer handle ID lookup if needed
                        var kvp = spellTable.Spells.FirstOrDefault(entry => entry.Value == s); 
                        return new { Id = kvp.Key, Name = s.Name ?? "?", DisplayText = $"{kvp.Key}: {s.Name ?? "?"}", Value = s };
                    })
                    .OrderBy(s => s.Id)
                    .ToList();
                
                itemsSource = filteredList;

                // Update message based on filtering result
                bool isFiltered = !string.IsNullOrWhiteSpace(nameFilter) || schoolFilter.HasValue || componentFilter.HasValue;
                if (isFiltered) {
                    _detailRenderer.ClearAndSetMessage($"Showing {filteredList.Count} matching spells. Select one.", isError: false);
                } else {
                    _detailRenderer.ClearAndSetMessage($"Listing all {filteredList.Count} spells. Select one.", isError: false);
                }
            }
            else 
            { 
                _detailRenderer.ClearAndSetMessage("No spells found.", isError: false); 
            }
            return itemsSource;
        }

        /// <summary>
        /// Checks if a file ID is likely the start of a range-based table.
        /// </summary>
        private bool IsRangeTableId(uint fileId)
        {
            // Add known range start IDs here
            return fileId == DatFileIds.ClothingTableId || fileId == DatFileIds.GfxObjTableId ||
                   fileId == DatFileIds.MotionTableId || fileId == DatFileIds.PaletteTableId ||
                   fileId == DatFileIds.ParticleEmitterTableId || fileId == DatFileIds.AnimationHookTableId ||
                   fileId == DatFileIds.ChatEmoteTableId || fileId == DatFileIds.DungeonTableId ||
                   fileId == DatFileIds.GeneratorTableId || fileId == DatFileIds.MaterialTableId ||
                   fileId == DatFileIds.QualityFilterTableId || fileId == DatFileIds.RenderMaterialTableId ||
                   fileId == DatFileIds.SoundTableId || fileId == DatFileIds.SurfaceTableId ||
                   fileId == DatFileIds.TextureTableId || fileId == DatFileIds.UILayoutTableId ||
                   fileId == DatFileIds.AnimationTableId || fileId == DatFileIds.LanguageTableId ||
                   fileId == DatFileIds.StringStateTableId;
        }

        /// <summary>
        /// Loads data based on a string tag identifier.
        /// </summary>
        private object? LoadDataForStringTag(string strTag, DatDatabase targetDb, string dbId, string displayName, out bool displayDirectly)
        {
            displayDirectly = false;
            object? itemsSource = null;
            Debug.WriteLine($"Processing node with string tag: {strTag}");

            var tagParts = strTag.Split(new[] { '_' }, 2);
            var baseTag = tagParts.Length > 0 ? tagParts[0] : strTag;
            var expectedDbId = tagParts.Length > 1 ? tagParts[1] : null;

            if (expectedDbId != null && expectedDbId != dbId)
            {
                 Debug.WriteLine($"Mismatched DB ID. Tag: {strTag}, Expected: {expectedDbId}, Actual: {dbId}");
                 _detailRenderer.ClearAndSetMessage($"Database ID mismatch for tag '{strTag}'.", isError: true);
                 return null;
            }

            if (targetDb is CellDatabase cellDb)
            {
                 bool handled = true;
                 switch (baseTag)
                 {
                    case "EnvCells": 
                        // --- DISABLED: Loading all EnvCells directly is too slow and causes issues ---
                        _detailRenderer.ClearAndAddDefaultTitle();
                        _detailRenderer.AddInfoMessage("Loading all EnvCells is disabled. Select a LandBlock first.", Colors.Orange);
                        itemsSource = null; // Ensure nothing is loaded into the list
                        displayDirectly = false;
                        handled = true; // Mark as handled to prevent GetPropertyDynamically fallback
                        break;
                    case "LandBlockInfos": 
                        // Keep lazy loading for these for now, assuming they are faster
                        itemsSource = cellDb.LandBlockInfos?.Select(x => new { DisplayText = $"LandBlockInfo 0x{x.Id:X8}", Id=x.Id, Value = x }); 
                        displayDirectly = false;
                        break;
                    case "LandBlocks": 
                        // Keep lazy loading for these for now, assuming they are faster
                        itemsSource = cellDb.LandBlocks?.Select(x => new { DisplayText = $"LandBlock 0x{x.Id:X8}", Id=x.Id, Value = x }); 
                        displayDirectly = false;
                        break;
                    default: handled = false; break;
                 }
                 // If handled is true, skip dynamic lookup
                 if (!handled) { itemsSource = GetPropertyDynamically(cellDb, baseTag); }

                 if (itemsSource != null)
                 {
                      _detailRenderer.ClearAndAddDefaultTitle();
                      int count = -1;
                      if (itemsSource is ICollection collection) {
                          count = collection.Count;
                      } else if (itemsSource is System.Collections.IEnumerable enumerable) {
                           // Use Count() extension method if available, might iterate but gets the job done.
                           // Need to cast to a specific IEnumerable<T> or use LINQ Count().
                           // Let's use LINQ Count() for simplicity, requires System.Linq namespace (already imported).
                           try { 
                                // Check if it's an IEnumerable<object> first for efficiency if possible
                                if (itemsSource is IEnumerable<object> objEnumerable) {
                                    count = objEnumerable.Count();
                                } else {
                                    // Fallback to casting to non-generic IEnumerable and counting manually (less efficient)
                                    count = enumerable.Cast<object>().Count(); 
                                }
                           } 
                           catch { 
                                count = -1; // Indicate count retrieval failed
                                Debug.WriteLine($"Could not determine count for IEnumerable of type {itemsSource.GetType().Name}");
                           }
                      }
                      _detailRenderer.AddInfoMessage($"Data loaded for {displayName}. {(count>=0 ? count.ToString() + " items." : "")} Select item from list if applicable.");
                 }
                 else { _detailRenderer.ClearAndSetMessage($"Could not load data for '{baseTag}' from Cell DB.", isError: true); }
            }
            else if (targetDb is PortalDatabase portalDbForProps)
            {
                itemsSource = GetPropertyDynamically(portalDbForProps, baseTag);
                 if (itemsSource != null)
                 {
                      _detailRenderer.ClearAndAddDefaultTitle();
                       int count = (itemsSource as ICollection)?.Count ?? -1;
                      _detailRenderer.AddInfoMessage($"Data loaded for {displayName}. {(count>=0 ? count.ToString() + " items." : "")} Select item from list if applicable.");
                 }
                 else { _detailRenderer.ClearAndSetMessage($"Could not load data for '{baseTag}' from Portal DB.", isError: true); }
            }
            else { _detailRenderer.ClearAndSetMessage($"Loading not implemented for '{baseTag}' in DB type {targetDb.GetType().Name}.", isError: true); }

            return itemsSource;
        }

        /// <summary>
        /// Updates the ListView or Detail Pane based on the loaded data.
        /// </summary>
        private void UpdateUIWithLoadedData(object? itemsSource, bool displayDirectly, string displayName)
        {
            if (displayDirectly && itemsSource != null)
            {
                 _itemListView.ItemsSource = null;
                 _detailRenderer.DisplayItemDetails(itemsSource);
                 Debug.WriteLine($"Displayed single object of type {itemsSource.GetType().Name} directly.");
            }
            else if (itemsSource != null)
            {
                if (itemsSource is IEnumerable enumerableSource && !(itemsSource is string))
                {
                    object? listSource = itemsSource;
                    if (listSource != null)
                    {
                        _itemListView.ItemsSource = listSource;
                        int count = (listSource as ICollection)?.Count ?? -1;
                        Debug.WriteLine($"ListView ItemsSource set. Count: {count}");
                         // Ensure Microsoft.UI.Colors is used
                         if (count >= 0) { _detailRenderer.AddInfoMessage($"{count} items loaded. Select from list.", Microsoft.UI.Colors.Green); }
                         else { _detailRenderer.AddInfoMessage("Items loaded. Select from list.", Microsoft.UI.Colors.Green); }
                    }
                    else { _itemListView.ItemsSource = null; }
                }
                else
                {
                     Debug.WriteLine($"ItemsSource not IEnumerable or is string. Type: {itemsSource.GetType().Name}");
                     _detailRenderer.ClearAndSetMessage($"Data '{displayName}' cannot be listed.", isError: true);
                     _itemListView.ItemsSource = null;
                }
            }
            else
            {
                 Debug.WriteLine($"ItemsSource is null for {displayName}.");
                 // Message added in loading logic
                 _itemListView.ItemsSource = null;
            }
        }

        /// <summary>
        /// Sets the UI to the completed/idle state.
        /// </summary>
        private void GoToCompletionState()
        {
            DataLoadingCompleted?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Helper to get a property value dynamically from an object by name.
        /// </summary>
        private object? GetPropertyDynamically(object source, string propertyName)
        {
             try
             {
                  var property = source.GetType().GetProperty(propertyName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.IgnoreCase);
                  if (property != null && property.CanRead)
                  {
                       var value = property.GetValue(source);
                       Debug.WriteLine($"Dynamically loaded property: {propertyName}, Type: {value?.GetType().Name ?? "null"}");
                       return value; // Return the value directly (IEnumerable, object, or null)
                  }
                  else
                  {
                       Debug.WriteLine($"Property {propertyName} not found or not readable on type {source.GetType().Name}");
                       _detailRenderer.ClearAndSetMessage($"Property '{propertyName}' not found.", isError: true);
                       return null;
                  }
             }
             catch (Exception ex)
             {
                  Debug.WriteLine($"Error getting property dynamically '{propertyName}': {ex.Message}");
                  _detailRenderer.ClearAndSetMessage($"Error accessing property '{propertyName}'.", isError: true);
                  return null;
             }
        }

        /// <summary>
        /// Extracts TreeNodeData from a given TreeViewNode.
        /// </summary>
        private TreeNodeData? ExtractNodeDataFromNode(TreeViewNode? treeNode)
        {
            if (treeNode == null) 
            {
                Debug.WriteLine("ExtractNodeDataFromNode: Received null node.");
                return null; 
            }
            
            Debug.WriteLine($"ExtractNodeDataFromNode: Processing node. Content type: {treeNode.Content?.GetType().Name}");

            if (treeNode.Content is TreeNodeData contentData)
            {
                Debug.WriteLine($"ExtractNodeDataFromNode: Found TreeNodeData. DisplayName: {contentData.DisplayName}");
                return contentData;
            }
            else
            {
                Debug.WriteLine("ExtractNodeDataFromNode: Node content is not TreeNodeData.");
                 // Optionally, try to fallback to last known good node if absolutely necessary?
                 // For now, just return null if the passed node isn't right.
                return null;
            }
        }

        /// <summary>
        /// Extracts the Database ID string from various identifier types.
        /// </summary>
        private string? GetDatabaseIdFromIdentifier(object? identifier)
        {
            return identifier switch
            {
                NodeIdentifier nodeId => nodeId.DatabaseId,
                string strId when strId.Contains('_') => strId.Split(new[] { '_' }, 2).LastOrDefault(),
                string strId => strId,
                _ => null
            };
        }

        /// <summary>
        /// Handles selection changes in the ItemListView to display details.
        /// </summary>
        private void ItemListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {            
            Debug.WriteLine("ItemListView_SelectionChanged triggered.");

            // --- Use Event Args to get selected item --- 
            object? actualSelectedItem = e.AddedItems.FirstOrDefault();
            
            if (actualSelectedItem == null)
            {
                Debug.WriteLine("ItemListView_SelectionChanged: AddedItems is empty or null.");
                // If selection is cleared (e.g., clicking empty space), AddedItems is empty.
                // You might want to clear the detail view here if desired.
                // _detailRenderer.ClearAndAddDefaultTitle(); 
                return;
            }
            // --- End Use Event Args --- 

            Debug.WriteLine($"ItemListView_SelectionChanged: Processing item type: {actualSelectedItem.GetType().Name}");
            
            // --- Get Database context FIRST --- 
            TreeNodeData? lastNode = GetLastSuccessfulNodeData(); // Get context from tree selection
            DatDatabase? relevantDb = null;
            string? dbId = GetDatabaseIdFromIdentifier(lastNode?.Identifier);
            if (!string.IsNullOrEmpty(dbId))
            {
                var dbInfo = _databaseManager.FindDatabase(dbId);
                relevantDb = dbInfo?.Database;
                if (relevantDb == null) { Debug.WriteLine($"ItemListView_SelectionChanged: Could not find database with ID {dbId} based on last tree node."); }
            }
            else { Debug.WriteLine("ItemListView_SelectionChanged: Could not determine DB ID from last tree node."); }
            // --- End Get Database --- 

            // --- Prepare Context for Detail Renderer ---
            Dictionary<string, object>? context = null;

            // --- Extract the actual data item ('Value') from the anonymous type --- 
            object? itemToProcess = actualSelectedItem;
            uint? selectedItemId = null; // Store ID if applicable
            
            var valuePropertyInfo = actualSelectedItem.GetType().GetProperty("Value");
            var idPropertyInfo = actualSelectedItem.GetType().GetProperty("Id"); // Get Id property too
            
            if (idPropertyInfo != null)
            {
                selectedItemId = idPropertyInfo.GetValue(actualSelectedItem) as uint?;
                
                // If we have an ID, store it in the context later
                if (selectedItemId.HasValue)
                {
                    if (context == null)
                    {
                        context = new Dictionary<string, object>();
                    }
                    context["SelectedItemId"] = selectedItemId.Value;
                    Debug.WriteLine($"ItemListView_SelectionChanged: Adding SelectedItemId={selectedItemId.Value} to context");
                }
            }
            
            if (valuePropertyInfo != null)
            {               
                var value = valuePropertyInfo.GetValue(actualSelectedItem);
                if (value != null) // If Value is NOT null, use it (e.g., for LandBlocks/Infos)
                {
                     itemToProcess = value;
                     Debug.WriteLine($"ItemListView_SelectionChanged: Extracted non-null 'Value' property. Processing type: {itemToProcess?.GetType().Name}");
                }
                else if (selectedItemId.HasValue && _lastSuccessfulNodeData?.DisplayName == "EnvCells")
                {                     
                    // Value is NULL, ID has value, and we selected from EnvCells list - Load the EnvCell now
                    Debug.WriteLine($"ItemListView_SelectionChanged: EnvCell placeholder selected (Id: 0x{selectedItemId.Value:X8}). Loading full object...");
                    if (relevantDb != null && relevantDb is CellDatabase cellDbForLoad) // Ensure we have the CellDatabase
                    {                       
                        if (cellDbForLoad.TryReadFile<EnvCell>(selectedItemId.Value, out var loadedEnvCell))
                        {
                            itemToProcess = loadedEnvCell;
                            Debug.WriteLine("ItemListView_SelectionChanged: Successfully loaded full EnvCell.");
                        }
                        else
                        {
                             Debug.WriteLine("ItemListView_SelectionChanged: Failed to load full EnvCell.");
                             _detailRenderer.ClearAndSetMessage($"Error: Could not load EnvCell 0x{selectedItemId.Value:X8}", true);
                             return; // Stop processing if load fails
                        }
                    }
                    else
                    {
                        Debug.WriteLine("ItemListView_SelectionChanged: Cannot load EnvCell - CellDatabase not available.");
                         _detailRenderer.ClearAndSetMessage("Error: Cell DB not found for EnvCell loading.", true);
                        return; // Stop processing
                    }
                }
                 else { Debug.WriteLine($"ItemListView_SelectionChanged: 'Value' property was null, but not loading EnvCell on demand."); }
            }
            else { Debug.WriteLine("ItemListView_SelectionChanged: Could not find 'Value' property, processing selected item directly."); }

            // If extraction failed, stop
            if (itemToProcess == null)
            {
                Debug.WriteLine("ItemListView_SelectionChanged: itemToProcess is null after attempting extraction.");
                return; 
            }
            // --- End Extraction --- 

            // --- SPECIAL: Handle LandBlock Selection to Load its EnvCells ---
            if (itemToProcess is LandBlock selectedLandBlock && relevantDb is CellDatabase cellDbForEnvCells)
            {
                uint landBlockId = selectedLandBlock.Id;
                uint envCellStartId = landBlockId | 0x00000001; // Assumed range start
                uint envCellEndId = landBlockId | 0x0000FFFF;   // Assumed range end

                Debug.WriteLine($"LandBlock 0x{landBlockId:X8} selected. Querying EnvCells in range 0x{envCellStartId:X8} - 0x{envCellEndId:X8}");
                _detailRenderer.AddInfoMessage($"Loading EnvCells for LandBlock 0x{landBlockId:X8}...", Colors.Orange);

                List<object>? envCellItemsSource = null;
                int envCellCount = 0;

                try
                {
                    if (cellDbForEnvCells.Tree != null)
                    {
                        var envCellFiles = cellDbForEnvCells.Tree.GetFilesInRange(envCellStartId, envCellEndId);
                        if (envCellFiles != null)
                        {
                            envCellItemsSource = envCellFiles.Select(f => new { DisplayText = $"EnvCell 0x{f.Id:X8}", Id = f.Id, Value = (EnvCell?)null }).ToList<object>();
                            envCellCount = envCellItemsSource.Count; // Get count from the generated list
                            Debug.WriteLine($"Found {envCellCount} EnvCells for LandBlock 0x{landBlockId:X8}.");
                        }
                        else
                        {
                            Debug.WriteLine($"BTree GetFilesInRange returned null for EnvCells for LandBlock 0x{landBlockId:X8}.");
                        }
                    }
                    else
                    {
                        Debug.WriteLine("CellDatabase.Tree is null. Cannot query EnvCells.");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error querying EnvCells for LandBlock 0x{landBlockId:X8}: {ex.Message}");
                    // Use AddInfoMessage with Red color for error
                    _detailRenderer.AddInfoMessage($"Error loading EnvCells: {ex.Message}", Colors.Red); 
                }

                // Update ListView and add message regardless of LandBlock details display
                _itemListView.ItemsSource = envCellItemsSource; // Set to null if no cells found or error
                if (envCellCount > 0)
                {
                     _detailRenderer.AddInfoMessage($"Found {envCellCount} EnvCells. Select one from the list.", Colors.Green);
                }
                else
                {
                     _detailRenderer.AddInfoMessage("No EnvCells found for this LandBlock.", Colors.Orange);
                }
                 // NOTE: We will fall through to display the LandBlock details below
            }
            // --- END SPECIAL LandBlock Handling ---

            // Try to get the correct PortalDatabase based on the last tree selection
            PortalDatabase? portalDbForContext = null;
            if (!string.IsNullOrEmpty(dbId))
            {
                var dbInfo = _databaseManager.FindDatabase(dbId);
                portalDbForContext = dbInfo?.Database as PortalDatabase;
            }

            // If we found the PortalDB, create the context and attempt to load all lookups
            if (portalDbForContext != null)
            {
                Debug.WriteLine("ItemListView_SelectionChanged: Found PortalDB, preparing context lookups.");
                // Initialize context if not already done
                if (context == null)
                {
                    context = new Dictionary<string, object>();
                }
                
                // Add DatabaseManager to context for spell editing
                context["DatabaseManager"] = _databaseManager;
                
                // Add DetailRenderer to context for updates
                context["DetailRenderer"] = _detailRenderer;
                
                // Add RefreshView action to context for UI updates after saving
                context["RefreshView"] = new Action(RefreshCurrentView);
                
                // Try to get the main window
                Window? mainWindow = WindowHelper.MainWindow;
                if (mainWindow != null)
                {
                    context["Window"] = mainWindow;
                }

                // Attempt to load all potential lookup sources
                portalDbForContext.TryReadFile<CharGen>(DatFileIds.CharGenId, out var charGenData);
                portalDbForContext.TryReadFile<SkillTable>(DatFileIds.SkillTableId, out var skillTableData);
                portalDbForContext.TryReadFile<SpellTable>(DatFileIds.SpellTableId, out var spellTableData);
                portalDbForContext.TryReadFile<SpellComponentTable>(DatFileIds.SpellComponentsTableId, out var spellComponentTableData);

                // Add ObjectType hint if the item is HeritageGroupCG (still needed for specific renderer selection)
                if (itemToProcess is HeritageGroupCG)
                {
                    context["ObjectType"] = "HeritageGroupCG";
                    Debug.WriteLine("ItemListView_SelectionChanged: Added ObjectType hint for HeritageGroupCG.");
                }
                // Add hints for other specific types if needed here...

                // Create and add SkillLookup
                if (skillTableData?.Skills != null)
                {
                    var skillLookup = skillTableData.Skills
                        .Where(kvp => kvp.Value?.Name != null)
                        .ToDictionary(kvp => (uint)kvp.Key, kvp => kvp.Value.Name ?? "?"); 
                    context["SkillLookup"] = skillLookup;
                    Debug.WriteLine($"ItemListView_SelectionChanged: Added SkillLookup ({skillLookup.Count} entries).");
                }
                else { Debug.WriteLine("ItemListView_SelectionChanged: SkillTable not loaded for SkillLookup."); }

                // Create and add StartAreaLookup
                if (charGenData?.StartingAreas != null)
                {
                    // Revert back to using the list index as the key for the lookup,
                    // as StartingArea doesn't have an explicit ID, and HeritageGroup references it by index.
                    var startAreaLookup = charGenData.StartingAreas
                        .Select((area, index) => new { Id = index, Name = area?.Name ?? "?" }) // Use index as ID
                        .Where(a => a.Name != "?") // Filter out any null areas or areas without names
                        .ToDictionary(a => a.Id, a => a.Name);
                    context["StartAreaLookup"] = startAreaLookup;
                    Debug.WriteLine($"ItemListView_SelectionChanged: Added StartAreaLookup ({startAreaLookup.Count} entries) using LIST INDEX as key.");
                }
                else { Debug.WriteLine("ItemListView_SelectionChanged: CharGen not loaded for StartAreaLookup."); }

                // Create and add SpellLookup
                if (spellTableData?.Spells != null)
                {
                    var spellLookup = spellTableData.Spells
                        .Where(kvp => kvp.Value?.Name != null)
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Name ?? "?");
                    context["SpellLookup"] = spellLookup;
                     Debug.WriteLine($"ItemListView_SelectionChanged: Added SpellLookup ({spellLookup.Count} entries).");
                }
                else { Debug.WriteLine("ItemListView_SelectionChanged: SpellTable not loaded for SpellLookup."); }

                // Create and add ComponentLookup
                if (spellComponentTableData?.Components != null)
                {
                    var componentLookup = spellComponentTableData.Components
                         .Where(kvp => kvp.Value?.Name != null)
                         .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Name ?? "?");
                     context["ComponentLookup"] = componentLookup;
                     Debug.WriteLine($"ItemListView_SelectionChanged: Added ComponentLookup ({componentLookup.Count} entries).");
                }
                else { Debug.WriteLine("ItemListView_SelectionChanged: SpellComponentTable not loaded for ComponentLookup."); }
            }
            else
            {
                 Debug.WriteLine("ItemListView_SelectionChanged: Could not find relevant PortalDatabase. No context lookups added.");
            }

            // --- End Context Preparation ---
            
            // --- Add SelectedItemId to context if applicable ---
            // Ensure context is initialized before trying to add to it
            if (context == null)
            {
                context = new Dictionary<string, object>();
            }
            
            if (actualSelectedItem != null)
            {
                var itemType = actualSelectedItem.GetType();
                var idProp = itemType.GetProperty("Id"); // Get the 'Id' property of the anonymous type
                if (idProp != null)
                {
                    var idValue = idProp.GetValue(actualSelectedItem);
                    if (idValue is EquipmentSet equipmentSetId) // Check if the ID is an EquipmentSet
                    {
                        context["SelectedItemId"] = equipmentSetId;
                        Debug.WriteLine($"ItemListView_SelectionChanged: Added SelectedItemId = {equipmentSetId} to context for SpellSet.");
                    }
                     else if (idValue is uint generalId) // Handle uint IDs (Spells, Components etc.)
                     {
                         context["SelectedItemId"] = generalId;
                         Debug.WriteLine($"ItemListView_SelectionChanged: Added SelectedItemId = {generalId} (uint) to context.");
                     }
                     // Add checks for other specific ID types if needed
                }
                 else
                 {                    
                     // It's possible the selected item doesn't have an 'Id' property (e.g., direct object display)
                     Debug.WriteLine("ItemListView_SelectionChanged: Could not find 'Id' property on selected item's anonymous type, or selected item is not the expected type.");
                 }
            }
            // --- End adding SelectedItemId ---

            // --- Special Handling for LanguageString Files from Range List (Keep existing logic) ---
            if (lastNode?.Identifier is NodeIdentifier nodeId && 
                nodeId.FileId == DatFileIds.LanguageTableId && 
                actualSelectedItem?.GetType().Name.Contains("AnonymousType") == true)
            {
                // Use itemToProcess (which should be the DatBTreeFile) 
                if (itemToProcess is DatBTreeFile fileEntry) 
                {
                    // Restore inline LanguageString loading logic
                    if (relevantDb != null && relevantDb is PortalDatabase portalDbForLang)
                    {
                        Debug.WriteLine($"Attempting to load selected LanguageString: 0x{fileEntry.Id:X8}");
                        _detailRenderer.ClearAndSetMessage($"Loading string 0x{fileEntry.Id:X8}...", isError: false);
                        if (portalDbForLang.TryReadFile<LanguageString>(fileEntry.Id, out var langString) && langString != null)
                        {
                            // Display the LanguageString object directly
                            _detailRenderer.DisplayItemDetails(langString);
                        }
                        else
                        {
                            _detailRenderer.ClearAndSetMessage($"Failed to read LanguageString file 0x{fileEntry.Id:X8}.", isError: true);
                        }
                    }
                    return; // Handled language string file selection
                }
            }
            // --- END Language String Handling ---

            // --- Get Item to Display ---
            // Start with the extracted item 
            object? itemToDisplay = itemToProcess; 

            // Check if the parent selection was the ExperienceTable
            if (_lastSuccessfulNodeData?.Identifier is NodeIdentifier expTableNodeId && expTableNodeId.FileId == DatFileIds.ExperienceTableId)
            {
                // Try to extract the 'Data' property from the anonymous type (actualSelectedItem)
                var dataProperty = actualSelectedItem?.GetType().GetProperty("Data");
                if (dataProperty != null)
                {
                    var dataValue = dataProperty.GetValue(actualSelectedItem);
                    if (dataValue != null) // Use the extracted array if found
                    {
                         System.Diagnostics.Debug.WriteLine($"ItemListView_SelectionChanged: ExperienceTable context detected. Extracted data array of type {dataValue.GetType().Name} to display.");
                         itemToDisplay = dataValue;

                         // --- Transform array into a list of indexed values --- 
                         if (itemToDisplay is uint[] uintArray)
                         {
                             itemToDisplay = uintArray.Select((val, idx) => new { Index = idx, Value = val }).ToList();
                             System.Diagnostics.Debug.WriteLine($"ItemListView_SelectionChanged: Transformed uint[] to List<{{Index, Value}}>.");
                         }
                         else if (itemToDisplay is ulong[] ulongArray)
                         {
                             itemToDisplay = ulongArray.Select((val, idx) => new { Index = idx, Value = val }).ToList();
                             System.Diagnostics.Debug.WriteLine($"ItemListView_SelectionChanged: Transformed ulong[] to List<{{Index, Value}}>.");
                         }
                         // --- End Transformation ---
                    }
                    else
                    {
                         System.Diagnostics.Debug.WriteLine("ItemListView_SelectionChanged: ExperienceTable context detected, but 'Data' property value was null.");
                    }
                }
                 else
                {
                      System.Diagnostics.Debug.WriteLine("ItemListView_SelectionChanged: ExperienceTable context detected, but failed to find 'Data' property on the selected item.");
                 }
            }
            // --- NEW: Check if the parent selection was the BadDataTable ---
            else if (_lastSuccessfulNodeData?.Identifier is NodeIdentifier badDataNodeId && badDataNodeId.FileId == DatFileIds.BadDataTableId)
            {
                // Try to extract the 'Value' property (the KeyValuePair) from the anonymous type (actualSelectedItem)
                var valueProperty = actualSelectedItem?.GetType().GetProperty("Value");
                if (valueProperty != null)
                {
                    var dataValue = valueProperty.GetValue(actualSelectedItem);
                    if (dataValue != null) // Use the extracted KeyValuePair if found
                    {
                        System.Diagnostics.Debug.WriteLine($"ItemListView_SelectionChanged: BadDataTable context detected. Extracted KeyValuePair to display.");
                        itemToDisplay = dataValue; // Display the KeyValuePair directly
                    }
                    else { System.Diagnostics.Debug.WriteLine("ItemListView_SelectionChanged: BadDataTable context detected, but 'Value' property value was null."); }
                }
                else { System.Diagnostics.Debug.WriteLine("ItemListView_SelectionChanged: BadDataTable context detected, but failed to find 'Value' property on the selected item."); }
            }
            // --- End BadDataTable Check ---
            // --- End Get Item to Display ---

            // Call DisplayItemDetails with the item extracted from 'Value' and the context dictionary
            _detailRenderer.DisplayItemDetails(itemToDisplay, context); 
        }

        /// <summary>
        /// Get the last successful node data
        /// </summary>
        public TreeNodeData? GetLastSuccessfulNodeData()
        {
            return _lastSuccessfulNodeData;
        }

        /// <summary>
        /// Helper to report on loaded databases for debugging
        /// </summary>
        private void LogLoadedDatabases()
        {
            Debug.WriteLine($"==== LOADED DATABASES ({_databaseManager.LoadedDatabases.Count}) ====");
            if (_databaseManager.LoadedDatabases.Count == 0) { Debug.WriteLine("No databases loaded"); return; }
            for (int i = 0; i < _databaseManager.LoadedDatabases.Count; i++)
            {
                var dbInfo = _databaseManager.LoadedDatabases[i];
                string dbId = _databaseManager.GetDatabaseId(dbInfo);
                Debug.WriteLine($"DB {i}: {dbInfo.FileName}, Type: {dbInfo.Type}, ID: {dbId}");
            }
            Debug.WriteLine($"Current DB: {(_databaseManager.CurrentDatabase != null ? "Set" : "Null")}, Type: {_databaseManager.CurrentDatabaseType}");
        }

        /// <summary>
        /// Refreshes the current view with the latest data
        /// </summary>
        public void RefreshCurrentView()
        {
            Debug.WriteLine("TreeViewDataLoader.RefreshCurrentView called");
            
            // Reprocess the last successful node to refresh the whole view
            if (_lastSuccessfulNodeData != null)
            {
                // Create a dummy TreeViewNode with the necessary data
                var dummyNode = new TreeViewNode
                {
                    Content = _lastSuccessfulNodeData
                };
                
                // Process the node asynchronously
                _ = ProcessTreeViewSelectionAsync(dummyNode);
            }
            else
            {
                Debug.WriteLine("RefreshCurrentView: No last successful node data available");
            }
        }
    }
} 