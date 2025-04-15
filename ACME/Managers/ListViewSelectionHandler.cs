using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using DatReaderWriter;
using DatReaderWriter.DBObjs;
using DatReaderWriter.Enums;
using DatReaderWriter.Types;
using DatReaderWriter.Lib.IO.DatBTree;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ACME.Constants;
using ACME.Models;
using ACME.Renderers;
using ACME.Helpers;

namespace ACME.Managers
{
    /// <summary>
    /// Handles the logic for processing selections in the ListView 
    /// and updating the DetailRenderer.
    /// </summary>
    public class ListViewSelectionHandler
    {
        private readonly DatabaseManager _databaseManager;
        private readonly DetailRenderer _detailRenderer;
        private readonly ListView _itemListView;

        public ListViewSelectionHandler(DatabaseManager databaseManager, DetailRenderer detailRenderer, ListView itemListView)
        {
            _databaseManager = databaseManager ?? throw new ArgumentNullException(nameof(databaseManager));
            _detailRenderer = detailRenderer ?? throw new ArgumentNullException(nameof(detailRenderer));
            _itemListView = itemListView ?? throw new ArgumentNullException(nameof(itemListView));
        }

        /// <summary>
        /// Handles selection changes in the ItemListView to display details.
        /// </summary>
        /// <param name="selectedItem">The newly selected item from the ListView (typically e.AddedItems.FirstOrDefault()).</param>
        /// <param name="treeContextNodeData">The TreeNodeData from the parent TreeView selection.</param>
        /// <param name="refreshAction">Action to refresh the current view.</param>
        public void HandleSelectionChanged(object? selectedItem, TreeNodeData? treeContextNodeData, Action refreshAction)
        {
            Debug.WriteLine("ListViewSelectionHandler.HandleSelectionChanged triggered.");

            object? actualSelectedItem = selectedItem;
            
            if (actualSelectedItem == null)
            {
                Debug.WriteLine("ListViewSelectionHandler: actualSelectedItem is null.");
                return;
            }

            Debug.WriteLine($"ListViewSelectionHandler: Processing item type: {actualSelectedItem.GetType().Name}");
            
            TreeNodeData? lastNode = treeContextNodeData; 
            DatDatabase? relevantDb = null;
            string? dbId = DatParsingHelpers.GetDatabaseIdFromIdentifier(lastNode?.Identifier); 
            if (!string.IsNullOrEmpty(dbId))
            {
                var dbInfo = _databaseManager.FindDatabase(dbId);
                relevantDb = dbInfo?.Database;
                if (relevantDb == null) { Debug.WriteLine($"ListViewSelectionHandler: Could not find database with ID {dbId} based on tree context node."); }
            }
            else { Debug.WriteLine("ListViewSelectionHandler: Could not determine DB ID from tree context node."); }

            Dictionary<string, object>? context = null;
            
            // Declare nodeIdentifier once here
            NodeIdentifier? nodeIdentifier = lastNode?.Identifier as NodeIdentifier; 

            object? itemToProcess = actualSelectedItem;
            uint? selectedItemId = null;
            
            var valuePropertyInfo = actualSelectedItem.GetType().GetProperty("Value");
            var idPropertyInfo = actualSelectedItem.GetType().GetProperty("Id");
            
            if (idPropertyInfo != null)
            {
                var idValue = idPropertyInfo.GetValue(actualSelectedItem);
                 if (idValue is uint uintId)
                 {
                     selectedItemId = uintId;
                     Debug.WriteLine($"ListViewSelectionHandler: Potential selectedItemId = 0x{selectedItemId.Value:X8}");
                 }
            }
            
            bool valueWasProcessed = false;
            if (valuePropertyInfo != null)
            {               
                var value = valuePropertyInfo.GetValue(actualSelectedItem);
                if (value != null)
                {
                    itemToProcess = value;
                    valueWasProcessed = true;
                    Debug.WriteLine($"ListViewSelectionHandler: Extracted non-null 'Value' property. Processing type: {itemToProcess?.GetType().Name}");
                }
            }

            if (!valueWasProcessed && selectedItemId.HasValue && nodeIdentifier != null) 
            {
                uint itemId = selectedItemId.Value;
                uint contextFileId = nodeIdentifier.FileId;
                DatDatabase? db = relevantDb;

                if (db == null) {
                    Debug.WriteLine($"ListViewSelectionHandler: Cannot lazy-load item 0x{itemId:X8} because relevant DB is null.");
                    _detailRenderer.ClearAndSetMessage($"Error: Cannot load details for 0x{itemId:X8}, database context lost.", true);
                    return;
                } 
                else if (contextFileId == DatFileIds.PaletteId && db is PortalDatabase portalDbForPaletteLoad) 
                {
                    Debug.WriteLine($"ListViewSelectionHandler: Palette placeholder selected (Id: 0x{itemId:X8}). Loading full object...");
                    if (portalDbForPaletteLoad.TryReadFile<Palette>(itemId, out var loadedPalette))
                    {
                        itemToProcess = loadedPalette;
                        Debug.WriteLine("ListViewSelectionHandler: Successfully loaded full Palette.");
                    }
                    else
                    {
                        Debug.WriteLine($"ListViewSelectionHandler: Failed to load full Palette 0x{itemId:X8}.");
                        _detailRenderer.ClearAndSetMessage($"Error: Could not load Palette 0x{itemId:X8}", true);
                        return;
                    }
                }
                else if (lastNode?.DisplayName == "EnvCells" && db is CellDatabase cellDbForLoad) 
                {                     
                    Debug.WriteLine($"ListViewSelectionHandler: EnvCell placeholder selected (Id: 0x{itemId:X8}). Loading full object...");
                    if (cellDbForLoad.TryReadFile<EnvCell>(itemId, out var loadedEnvCell))
                    {
                        itemToProcess = loadedEnvCell;
                        Debug.WriteLine("ListViewSelectionHandler: Successfully loaded full EnvCell.");
                    }
                    else
                    {
                        Debug.WriteLine($"ListViewSelectionHandler: Failed to load full EnvCell 0x{itemId:X8}.");
                        _detailRenderer.ClearAndSetMessage($"Error: Could not load EnvCell 0x{itemId:X8}", true);
                        return;
                    }
                } 
                else 
                {
                    Debug.WriteLine($"ListViewSelectionHandler: Item 0x{itemId:X8} with missing Value from context {contextFileId:X8} doesn't match known lazy-load types. Processing placeholder.");
                }
            } else if (!valueWasProcessed) {
                Debug.WriteLine($"ListViewSelectionHandler: Could not find 'Value' property or necessary context/ID for lazy loading. Processing selected item directly: {itemToProcess?.GetType().Name}");
            }

            if (itemToProcess == null)
            {
                Debug.WriteLine("ListViewSelectionHandler: itemToProcess is null after attempting extraction.");
                 _detailRenderer.ClearAndSetMessage("Error processing selected item.", true);
                return; 
            }

            if (itemToProcess is LandBlock selectedLandBlock && relevantDb is CellDatabase cellDbForEnvCells)
            {
                uint landBlockId = selectedLandBlock.Id;
                uint envCellStartId = landBlockId | 0x00000001;
                uint envCellEndId = landBlockId | 0x0000FFFF;

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
                            envCellItemsSource = envCellFiles
                                .Select(f => new { DisplayText = $"EnvCell 0x{f.Id:X8}", Id = f.Id, Value = (EnvCell?)null })
                                .OrderBy(p => p.Id)
                                .ToList<object>();
                            envCellCount = envCellItemsSource.Count;
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
                    _detailRenderer.AddInfoMessage($"Error loading EnvCells: {ex.Message}", Colors.Red); 
                }

                _itemListView.ItemsSource = envCellItemsSource;
                
                if (envCellCount > 0)
                {
                     _detailRenderer.AddInfoMessage($"Found {envCellCount} EnvCells. Select one from the list.", Colors.Green);
                }
                else
                {
                     _detailRenderer.AddInfoMessage("No EnvCells found for this LandBlock.", Colors.Orange);
                }
            }

            object? itemToDisplay = itemToProcess; 

            if (itemToDisplay is DatBTreeFile rangeFileEntry && 
                nodeIdentifier != null &&
                DatParsingHelpers.IsRangeTableId(nodeIdentifier.FileId) &&
                relevantDb is PortalDatabase portalDbForRangeLoad)
            {
                uint fileIdToLoad = rangeFileEntry.Id; 
                uint originalRangeId = nodeIdentifier.FileId;
                object? loadedDbObj = null;

                Debug.WriteLine($"ListViewSelectionHandler: Range item 0x{fileIdToLoad:X8} selected from original range ID 0x{originalRangeId:X8}. Attempting to load actual DBObj...");

                bool loadSuccess = false;
                 if (originalRangeId == DatFileIds.LanguageStringDataId)
                 {
                     Debug.WriteLine($"ListViewSelectionHandler: Attempting to load selected LanguageString: 0x{fileIdToLoad:X8}");
                     if (portalDbForRangeLoad.TryReadFile<LanguageString>(fileIdToLoad, out var langString) && langString != null)
                     {
                         loadedDbObj = langString;
                         loadSuccess = true;
                     }
                     else
                     {
                          _detailRenderer.ClearAndSetMessage($"Failed to read LanguageString file 0x{fileIdToLoad:X8}.", isError: true);
                     }
                 }
                 else
                 {
                     switch (originalRangeId)
                     {
                        case DatFileIds.AnimationId:
                            if (portalDbForRangeLoad.TryReadFile<Animation>(fileIdToLoad, out var anim)) { loadedDbObj = anim; loadSuccess = true; } break;
                        case DatFileIds.GfxObjId:
                            if (portalDbForRangeLoad.TryReadFile<GfxObj>(fileIdToLoad, out var gfx)) { loadedDbObj = gfx; loadSuccess = true; } break;
                        case DatFileIds.ClothingTableId:
                            if (portalDbForRangeLoad.TryReadFile<Clothing>(fileIdToLoad, out var clothing)) { loadedDbObj = clothing; loadSuccess = true; } break;
                        case DatFileIds.PaletteId:
                            if (portalDbForRangeLoad.TryReadFile<Palette>(fileIdToLoad, out var pal)) { loadedDbObj = pal; loadSuccess = true; } break;
                        case DatFileIds.SurfaceId:
                            if (portalDbForRangeLoad.TryReadFile<Surface>(fileIdToLoad, out var surf)) { loadedDbObj = surf; loadSuccess = true; } break;
                        case DatFileIds.SurfaceTextureId:
                            if (portalDbForRangeLoad.TryReadFile<SurfaceTexture>(fileIdToLoad, out var tex)) { loadedDbObj = tex; loadSuccess = true; } break;
                        case DatFileIds.MotionTableId:
                            if (portalDbForRangeLoad.TryReadFile<MotionTable>(fileIdToLoad, out var mot)) { loadedDbObj = mot; loadSuccess = true; } break;
                        case DatFileIds.RenderSurfaceId:
                            if (portalDbForRangeLoad.TryReadFile<RenderSurface>(fileIdToLoad, out var rs)) { loadedDbObj = rs; loadSuccess = true; } break;
                        case DatFileIds.SoundTableId:
                            if (portalDbForRangeLoad.TryReadFile<Wave>(fileIdToLoad, out var wave)) { loadedDbObj = wave; loadSuccess = true; } break;
                        case DatFileIds.MaterialId:
                            if (portalDbForRangeLoad.TryReadFile<MaterialInstance>(fileIdToLoad, out var mat)) { loadedDbObj = mat; loadSuccess = true; } break;
                        case DatFileIds.ParticleEmitterTableId:
                            if (portalDbForRangeLoad.TryReadFile<ParticleEmitter>(fileIdToLoad, out var emitter)) { loadedDbObj = emitter; loadSuccess = true; } break;
                        case DatFileIds.EnvironmentId:
                            if (portalDbForRangeLoad.TryReadFile<DatReaderWriter.DBObjs.Environment>(fileIdToLoad, out var env)) { loadedDbObj = env; loadSuccess = true; } break;
                        default:
                            Debug.WriteLine($"ListViewSelectionHandler: No specific loader implemented for range ID 0x{originalRangeId:X8}. Cannot load concrete object.");
                            break;
                     }
                 }

                if (loadSuccess && loadedDbObj != null) 
                {
                    itemToDisplay = loadedDbObj;
                    Debug.WriteLine($"ListViewSelectionHandler: Successfully loaded {loadedDbObj.GetType().Name} (0x{fileIdToLoad:X8}) for display.");
                }
                else if (!loadSuccess && originalRangeId != DatFileIds.LanguageStringDataId)
                {
                    Debug.WriteLine($"ListViewSelectionHandler: Failed to load actual object for range item 0x{fileIdToLoad:X8}. Displaying BTreeFile entry.");
                    _detailRenderer.AddInfoMessage($"Failed to load file 0x{fileIdToLoad:X8}. Showing file details only.", Colors.Orange);
                }
                 else if (originalRangeId == DatFileIds.LanguageStringDataId && loadedDbObj == null)
                 {
                      Debug.WriteLine($"ListViewSelectionHandler: Failed to load LanguageString 0x{fileIdToLoad:X8}. Displaying BTreeFile entry.");
                 }
            }

            if (lastNode?.Identifier is NodeIdentifier expTableNodeId && expTableNodeId.FileId == DatFileIds.ExperienceTableId)
            {
                var dataProperty = actualSelectedItem?.GetType().GetProperty("Data");
                if (dataProperty != null)
                {
                    var dataValue = dataProperty.GetValue(actualSelectedItem);
                    if (dataValue != null)
                    {
                         Debug.WriteLine($"ListViewSelectionHandler: ExperienceTable context detected. Extracted data array of type {dataValue.GetType().Name} to display.");
                         itemToDisplay = dataValue;

                         if (itemToDisplay is uint[] uintArray)
                         {
                             itemToDisplay = uintArray.Select((val, idx) => new { Index = idx, Value = val }).ToList();
                             Debug.WriteLine($"ListViewSelectionHandler: Transformed uint[] to List<{{Index, Value}}>.");
                         }
                         else if (itemToDisplay is ulong[] ulongArray)
                         {
                             itemToDisplay = ulongArray.Select((val, idx) => new { Index = idx, Value = val }).ToList();
                             Debug.WriteLine($"ListViewSelectionHandler: Transformed ulong[] to List<{{Index, Value}}>.");
                         }
                    }
                    else { Debug.WriteLine("ListViewSelectionHandler: ExperienceTable context detected, but 'Data' property value was null."); }
                }
                 else { Debug.WriteLine("ListViewSelectionHandler: ExperienceTable context detected, but failed to find 'Data' property on the selected item."); }
            }

            PortalDatabase? portalDbForContext = relevantDb as PortalDatabase;

            if (portalDbForContext != null)
            {
                Debug.WriteLine("ListViewSelectionHandler: Found PortalDB, preparing context lookups.");
                context = new Dictionary<string, object>();
                
                context["DatabaseManager"] = _databaseManager;
                context["DetailRenderer"] = _detailRenderer;
                context["RefreshView"] = refreshAction;
                
                Window? mainWindow = WindowHelper.MainWindow;
                if (mainWindow != null) { context["Window"] = mainWindow; }

                portalDbForContext.TryReadFile<CharGen>(DatFileIds.CharGenId, out var charGenData);
                portalDbForContext.TryReadFile<SkillTable>(DatFileIds.SkillTableId, out var skillTableData);
                portalDbForContext.TryReadFile<SpellTable>(DatFileIds.SpellTableId, out var spellTableData);
                portalDbForContext.TryReadFile<SpellComponentTable>(DatFileIds.SpellComponentsTableId, out var spellComponentTableData);

                if (itemToDisplay is HeritageGroupCG) { context["ObjectType"] = "HeritageGroupCG"; }

                if (skillTableData?.Skills != null)
                {
                    var skillLookup = skillTableData.Skills.Where(kvp => kvp.Value?.Name != null).ToDictionary(kvp => (uint)kvp.Key, kvp => kvp.Value.Name ?? "?"); 
                    context["SkillLookup"] = skillLookup;
                    Debug.WriteLine($"ListViewSelectionHandler: Added SkillLookup ({skillLookup.Count} entries).");
                } else { Debug.WriteLine("ListViewSelectionHandler: SkillTable not loaded for SkillLookup."); }

                if (charGenData?.StartingAreas != null)
                {
                    var startAreaLookup = charGenData.StartingAreas.Select((area, index) => new { Id = index, Name = area?.Name ?? "?" }).Where(a => a.Name != "?").ToDictionary(a => a.Id, a => a.Name);
                    context["StartAreaLookup"] = startAreaLookup;
                    Debug.WriteLine($"ListViewSelectionHandler: Added StartAreaLookup ({startAreaLookup.Count} entries) using LIST INDEX as key.");
                } else { Debug.WriteLine("ListViewSelectionHandler: CharGen not loaded for StartAreaLookup."); }

                if (spellTableData?.Spells != null)
                {
                    var spellLookup = spellTableData.Spells.Where(kvp => kvp.Value?.Name != null).ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Name ?? "?");
                    context["SpellLookup"] = spellLookup;
                     Debug.WriteLine($"ListViewSelectionHandler: Added SpellLookup ({spellLookup.Count} entries).");
                } else { Debug.WriteLine("ListViewSelectionHandler: SpellTable not loaded for SpellLookup."); }

                if (spellComponentTableData?.Components != null)
                {
                    var componentLookup = spellComponentTableData.Components.Where(kvp => kvp.Value?.Name != null).ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Name ?? "?");
                     context["ComponentLookup"] = componentLookup;
                     Debug.WriteLine($"ListViewSelectionHandler: Added ComponentLookup ({componentLookup.Count} entries).");
                } else { Debug.WriteLine("ListViewSelectionHandler: SpellComponentTable not loaded for ComponentLookup."); }

                if (actualSelectedItem != null)
                {
                    var itemType = actualSelectedItem.GetType();
                    var idProp = itemType.GetProperty("Id"); 
                    if (idProp != null)
                    {
                        var idValue = idProp.GetValue(actualSelectedItem);
                        if (idValue is EquipmentSet equipmentSetId)
                        {
                            context["SelectedItemId"] = equipmentSetId;
                            Debug.WriteLine($"ListViewSelectionHandler: Added SelectedItemId = {equipmentSetId} (EquipmentSet) to context.");
                        }
                        else if (idValue is uint generalId)
                        {
                            if (!context.ContainsKey("SelectedItemId")) 
                            {
                                context["SelectedItemId"] = generalId;
                                Debug.WriteLine($"ListViewSelectionHandler: Added SelectedItemId = {generalId} (uint) to context.");
                            } else {
                                Debug.WriteLine($"ListViewSelectionHandler: SelectedItemId context key already exists (Value: {context["SelectedItemId"]}), not overwriting with uint ID {generalId}.");
                            }
                        }
                    }
                      else { Debug.WriteLine("ListViewSelectionHandler: Could not find 'Id' property on selected item's anonymous type."); }
                }
            }
            else
            {
                 Debug.WriteLine("ListViewSelectionHandler: Could not find relevant PortalDatabase. Limited context prepared.");
                 context = new Dictionary<string, object>();
                 context["DatabaseManager"] = _databaseManager;
                 context["DetailRenderer"] = _detailRenderer;
                 context["RefreshView"] = refreshAction; 
                 Window? mainWindow = WindowHelper.MainWindow;
                 if (mainWindow != null) { context["Window"] = mainWindow; }
            }

            // --- BEGIN ADD DATABASE TO CONTEXT ---
            // Ensure context is initialized
            context ??= new Dictionary<string, object>();

            // Add the database instance if available
            if (relevantDb != null)
            {
                if (!context.ContainsKey("Database"))
                {
                    context.Add("Database", relevantDb);
                    Debug.WriteLine($"ListViewSelectionHandler: Added Database (Type: {relevantDb.GetType().Name}) to context.");
                }
                else
                {
                    Debug.WriteLine("ListViewSelectionHandler: Context already contains a 'Database' key.");
                }
            }
            else
            {
                Debug.WriteLine("ListViewSelectionHandler: relevantDb is null, cannot add Database to context.");
            }
            // --- END ADD DATABASE TO CONTEXT ---

            _detailRenderer.DisplayItemDetails(itemToDisplay, context); 
        }
    }
} 