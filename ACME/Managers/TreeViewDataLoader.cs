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
        // Constants for magic strings
        private const string SubtypeSpells = "Spells";
        private const string SubtypeSpellSets = "SpellSets";
        private const string SubtypeChatPoses = "ChatPoses";
        private const string SubtypeChatEmotes = "ChatEmotes";
        private const string SubtypeStartingAreas = "StartingAreas";
        private const string SubtypeHeritageGroups = "HeritageGroups";

        private const string TagEnvCells = "EnvCells";
        private const string TagLandBlockInfos = "LandBlockInfos";
        private const string TagLandBlocks = "LandBlocks";

        private readonly DatabaseManager _databaseManager;
        private readonly ListView _itemListView;
        private readonly DetailRenderer _detailRenderer;
        private readonly ListViewSelectionHandler _listViewSelectionHandler;
        private readonly SpellFilterManager _spellFilterManager;
        private readonly SpellLoader _spellLoader;
        
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
        
        public TreeViewDataLoader(
            DatabaseManager databaseManager,
            ListView itemListView,
            DetailRenderer detailRenderer,
            ListViewSelectionHandler listViewSelectionHandler,
            SpellFilterManager spellFilterManager,
            SpellLoader spellLoader)
        {
            _databaseManager = databaseManager ?? throw new ArgumentNullException(nameof(databaseManager));
            _itemListView = itemListView ?? throw new ArgumentNullException(nameof(itemListView));
            _detailRenderer = detailRenderer ?? throw new ArgumentNullException(nameof(detailRenderer));
            
            _listViewSelectionHandler = listViewSelectionHandler ?? throw new ArgumentNullException(nameof(listViewSelectionHandler));
            _spellFilterManager = spellFilterManager ?? throw new ArgumentNullException(nameof(spellFilterManager));
            _spellLoader = spellLoader ?? throw new ArgumentNullException(nameof(spellLoader));
            
            _itemListView.SelectionChanged += ItemListView_SelectionChanged;
        }
        
        /// <summary>
        /// Process the selected TreeView item and load appropriate data
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
                string? dbId = DatParsingHelpers.GetDatabaseIdFromIdentifier(nodeData.Identifier);
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
            bool isSpellViewRelevant = false; // Initialize relevance flag
            Debug.WriteLine($"Processing node with FileId: 0x{fileId:X8}, Subtype: {subtype}");

            // Determine if we are loading spells first, to clear the filter manager if not.
            bool isLoadingSpellsOrSets = fileId == DatFileIds.SpellTableId; // Check if it's the SpellTable ID

            if (!isLoadingSpellsOrSets)
            {
                 _spellFilterManager.SetSpellTable(null); // Clear manager if not loading spells/sets
                 isSpellViewRelevant = false;
                 RelevantDataViewChanged?.Invoke(this, new RelevantDataViewChangedEventArgs(isSpellViewRelevant));
            }
            // Else: Relevance for spell view will be determined by SpellLoader below.

            if (targetDb is PortalDatabase portalDb)
            {
                 _detailRenderer.ClearAndSetMessage($"Attempting to load data for {displayName}...", isError: false); // Generic loading message

                 // --- Use SpellLoader for SpellTableId --- (Handles Spells and SpellSets)
                if (isLoadingSpellsOrSets)
                {
                    if (string.IsNullOrEmpty(subtype) || subtype == SubtypeSpells)
                    {
                        // Use LoadSpellsAndFilter which returns the result object
                        var filterResult = _spellLoader.LoadSpellsAndFilter(portalDb);
                        itemsSource = filterResult.FilteredItemsSource;
                        isSpellViewRelevant = filterResult.HasData;

                        _detailRenderer.ClearAndAddDefaultTitle(); // Clear previous messages
                        if (filterResult.HasData)
                        {
                             _detailRenderer.AddInfoMessage($"Total Spells (Unfiltered): {_spellFilterManager.GetTotalSpellCount()}");
                             _detailRenderer.AddInfoMessage(filterResult.StatusMessage); // Display status from filter result
                        }
                        else
                        {
                            _detailRenderer.ClearAndSetMessage(filterResult.StatusMessage, isError: true); // Show error/status if no data
                        }
                    }
                    else if (subtype == SubtypeSpellSets)
                    {
                        itemsSource = _spellLoader.LoadSpellSets(portalDb);
                        isSpellViewRelevant = false; // Spell sets don't use the spell filter view

                        _detailRenderer.ClearAndAddDefaultTitle();
                        if (itemsSource != null && itemsSource is ICollection setCollection && setCollection.Count > 0)
                        {
                             _detailRenderer.AddInfoMessage($"Total Spell Sets: {setCollection.Count}");
                             _detailRenderer.AddInfoMessage("Select a spell set from the list.");
                        }
                        else if (itemsSource == null)
                        {
                             // Error loading spell sets (SpellLoader failed to read table)
                            _detailRenderer.ClearAndSetMessage("Failed to load SpellTable data for SpellSets.", isError: true);
                        }
                        else // Empty list returned
                        {
                             _detailRenderer.AddInfoMessage("No spell sets found in the table.");
                        }
                    }
                    else // Unknown subtype for SpellTableId
                    {
                        _detailRenderer.ClearAndSetMessage($"Unknown SpellTable subtype: {subtype}", isError: true);
                        itemsSource = null;
                        isSpellViewRelevant = false;
                    }
                    // Raise the event after handling all SpellTable subtypes
                    RelevantDataViewChanged?.Invoke(this, new RelevantDataViewChangedEventArgs(isSpellViewRelevant));
                }
                // --- Keep other file ID handling directly in this class --- (Ensure this ELSE is correct)
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
                        if (subtype == SubtypeChatPoses)
                        {
                            _detailRenderer.ClearAndAddDefaultTitle();
                            if (chatPoseTable.ChatPoseHash != null && chatPoseTable.ChatPoseHash.Count > 0)
                            {
                                itemsSource = chatPoseTable.ChatPoseHash
                                    .Select(kvp => new { DisplayText = kvp.Key, Value = kvp }) // Pass the whole KeyValuePair as the Value
                                    .OrderBy(p => p.DisplayText)
                                    .ToList();
                                _detailRenderer.ClearAndSetMessage($"Listing Chat Poses ({chatPoseTable.ChatPoseHash.Count} found). Select one from the list.", isError: false);
                            }
                            displayDirectly = false; // List the dictionary entries first
                        }
                        else if (subtype == SubtypeChatEmotes)
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
                // --- CharGen with Subtypes ---
                 else if (fileId == DatFileIds.CharGenId)
                 {
                     if (portalDb.TryReadFile<CharGen>(fileId, out var charGen) && charGen != null)
                     {
                         _detailRenderer.ClearAndSetMessage("Successfully read CharGen file", isError: false);
                         if (subtype == SubtypeStartingAreas)
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
                         else if (subtype == SubtypeHeritageGroups)
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
                 // --- Handle PaletteSetId specifically ---
                 else if (fileId == DatFileIds.PaletteSetId)
                 {
                     displayDirectly = false;
                     try
                     {
                         _detailRenderer.ClearAndAddDefaultTitle();
                         if (portalDb.PaletteSets != null && portalDb.PaletteSets.Count() > 0)
                         {
                            // Assuming PaletteSet has an Id property. Adjust if needed.
                            itemsSource = portalDb.PaletteSets
                                .Select(ps => new { Id = ps.Id, DisplayText = $"PaletteSet 0x{ps.Id:X8}", Value = ps })
                                .OrderBy(ps => ps.Id)
                                .ToList();
                            _detailRenderer.AddInfoMessage($"Found {portalDb.PaletteSets.Count()} Palette Sets. Select one from the list.");
                         }
                         else
                         {
                            _detailRenderer.AddInfoMessage("No Palette Sets found.");
                            itemsSource = null;
                         }
                     }
                     catch (Exception psEx)
                     { Debug.WriteLine($"Error getting Palette Sets: {psEx.Message}"); _detailRenderer.ClearAndSetMessage("Error listing Palette Sets.", isError: true); itemsSource = null; }
                 }
                 // --- Generic File Range Tables (Catch-all for other ranges) ---
                 else if (DatParsingHelpers.IsRangeTableId(fileId))
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
            Debug.WriteLine($"ApplySpellFilter called with Name: '{nameFilter}'");

            // Use the spell filter manager
            var filterResult = _spellFilterManager.ApplyFilters(nameFilter); // <-- Use SpellFilterManager

            // Update the ListView's ItemsSource directly
            _itemListView.ItemsSource = filterResult.FilteredItemsSource;

            // Clear previous messages and set the new status message
            _detailRenderer.ClearAndSetMessage(filterResult.StatusMessage, isError: false); // Use ClearAndSetMessage

            // Ensure relevance is still true when applying filter (if the manager has data)
            // We infer relevance from whether the filter manager produced data.
            RelevantDataViewChanged?.Invoke(this, new RelevantDataViewChangedEventArgs(filterResult.HasData));
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
                    case TagEnvCells: 
                        // --- DISABLED: Loading all EnvCells directly is too slow and causes issues ---
                        _detailRenderer.ClearAndAddDefaultTitle();
                        _detailRenderer.AddInfoMessage("Loading all EnvCells is disabled. Select a LandBlock first.", Colors.Orange);
                        itemsSource = null; // Ensure nothing is loaded into the list
                        displayDirectly = false;
                        handled = true; // Mark as handled to prevent GetPropertyDynamically fallback
                        break;
                    case TagLandBlockInfos: 
                        // Keep lazy loading for these for now, assuming they are faster
                        itemsSource = cellDb.LandBlockInfos?.Select(x => new { DisplayText = $"LandBlockInfo 0x{x.Id:X8}", Id=x.Id, Value = x }); 
                        displayDirectly = false;
                        break;
                    case TagLandBlocks: 
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

            // Ensure spell view is marked as irrelevant when loading non-spell string tags
            _spellFilterManager.SetSpellTable(null);
            RelevantDataViewChanged?.Invoke(this, new RelevantDataViewChangedEventArgs(false));

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
        /// Handles selection changes in the ItemListView to display details.
        /// Now delegates the core logic to ListViewSelectionHandler.
        /// </summary>
        private void ItemListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {            
            Debug.WriteLine("ItemListView_SelectionChanged triggered (delegating to handler).");

            // Get the actual selected item from the event args
            object? actualSelectedItem = e.AddedItems.FirstOrDefault();

            // Call the handler method, passing the selected item, the current tree context, and the refresh action
            _listViewSelectionHandler.HandleSelectionChanged(actualSelectedItem, _lastSuccessfulNodeData, RefreshCurrentView);
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