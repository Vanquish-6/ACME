using DatReaderWriter;
using DatReaderWriter.DBObjs;
using DatReaderWriter.Enums;
using ACME.Constants;
using ACME.Renderers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ACME.Managers
{
    /// <summary>
    /// Handles loading of spell data.
    /// </summary>
    public class SpellLoader
    {
        private readonly SpellFilterManager _spellFilterManager;
        private readonly DetailRenderer _detailRenderer;

        public SpellLoader(SpellFilterManager spellFilterManager, DetailRenderer detailRenderer)
        {
            _spellFilterManager = spellFilterManager ?? throw new ArgumentNullException(nameof(spellFilterManager));
            _detailRenderer = detailRenderer ?? throw new ArgumentNullException(nameof(detailRenderer));
        }

        /// <summary>
        /// Loads spell data (Spells or SpellSets) from the given database.
        /// </summary>
        /// <param name="portalDb">The portal database instance.</param>
        /// <param name="subtype">The specific subtype requested (e.g., "Spells", "SpellSets").</param>
        /// <param name="isRelevant">Output parameter indicating if the loaded data is relevant for spell-specific UI.</param>
        /// <returns>The itemsSource object for the ListView, or null if loading failed.</returns>
        public object? LoadSpellData(PortalDatabase portalDb, string subtype, out bool isRelevant)
        {
            object? itemsSource = null;
            isRelevant = false;
            uint fileId = DatFileIds.SpellTableId;

            Debug.WriteLine($"SpellLoader: Attempting to load SpellTable with subtype: {subtype}");

            if (portalDb.TryReadFile<SpellTable>(fileId, out var spellTable) && spellTable != null)
            {
                Debug.WriteLine($"SpellLoader: Successfully read SpellTable.");

                if (string.IsNullOrEmpty(subtype) || subtype == "Spells")
                {
                    _spellFilterManager.SetSpellTable(spellTable);

                    var filterResult = _spellFilterManager.ApplyFilters("");
                    itemsSource = filterResult.FilteredItemsSource;
                    isRelevant = filterResult.HasData;
                    Debug.WriteLine($"SpellLoader: Loaded Spells. Relevant: {isRelevant}");
                }
                else if (subtype == "SpellSets")
                {
                    if (spellTable.SpellsSets != null && spellTable.SpellsSets.Count > 0)
                    {
                        itemsSource = spellTable.SpellsSets
                            .Select(s => new { Id = s.Key, Count = s.Value?.SpellSetTiers?.Count ?? 0, DisplayText = $"{s.Key}: Set ({s.Value?.SpellSetTiers?.Count ?? 0} tiers)", Value = s.Value })
                            .OrderBy(s => s.Id)
                            .ToList();
                    }
                    isRelevant = false;
                    Debug.WriteLine($"SpellLoader: Loaded SpellSets. Relevant: {isRelevant}");
                }
                else
                {
                    isRelevant = false;
                    Debug.WriteLine($"SpellLoader: Unknown subtype '{subtype}'. Relevant: {isRelevant}");
                    itemsSource = null;
                }
            }
            else
            {
                isRelevant = false;
                 Debug.WriteLine($"SpellLoader: Failed to load SpellTable. Relevant: {isRelevant}");
                 itemsSource = null;
            }

            return itemsSource;
        }

        public SpellFilterResult GetInitialFilterResult()
        {
             Debug.WriteLine("SpellLoader.GetInitialFilterResult called - returning default.");
            return new SpellFilterResult(null, "Filter result not available directly.", false);
        }

         /// <summary>
        /// Loads spell data (Spells only) and returns the filter result.
        /// </summary>
        public SpellFilterResult LoadSpellsAndFilter(PortalDatabase portalDb)
        {
            uint fileId = DatFileIds.SpellTableId;
            SpellFilterResult result;

            Debug.WriteLine($"SpellLoader: Attempting to load SpellTable for filtering.");

            if (portalDb.TryReadFile<SpellTable>(fileId, out var spellTable) && spellTable != null)
            {
                Debug.WriteLine($"SpellLoader: Successfully read SpellTable for filtering.");
                _spellFilterManager.SetSpellTable(spellTable);
                result = _spellFilterManager.ApplyFilters("");
                Debug.WriteLine($"SpellLoader: Loaded Spells via LoadSpellsAndFilter. Relevant: {result.HasData}");
            }
            else
            {
                 Debug.WriteLine($"SpellLoader: Failed to load SpellTable for filtering.");
                 _spellFilterManager.SetSpellTable(null);
                 result = new SpellFilterResult(null, "Failed to load SpellTable data.", false);
            }
            return result;
        }

         /// <summary>
        /// Loads spell sets data.
        /// </summary>
        public object? LoadSpellSets(PortalDatabase portalDb)
        {
             uint fileId = DatFileIds.SpellTableId;
             object? itemsSource = null;
             Debug.WriteLine($"SpellLoader: Attempting to load SpellSets.");

             if (portalDb.TryReadFile<SpellTable>(fileId, out var spellTable) && spellTable != null)
             {
                 Debug.WriteLine($"SpellLoader: Successfully read SpellTable for SpellSets.");
                 if (spellTable.SpellsSets != null && spellTable.SpellsSets.Count > 0)
                 {
                     itemsSource = spellTable.SpellsSets
                         .Select(s => new { Id = s.Key, Count = s.Value?.SpellSetTiers?.Count ?? 0, DisplayText = $"{s.Key}: Set ({s.Value?.SpellSetTiers?.Count ?? 0} tiers)", Value = s.Value })
                         .OrderBy(s => s.Id)
                         .ToList();
                     Debug.WriteLine($"SpellLoader: Found {spellTable.SpellsSets.Count} SpellSets.");
                 }
                 else
                 {
                     Debug.WriteLine($"SpellLoader: No SpellSets found in table.");
                      itemsSource = null;
                 }
             }
             else
             {
                  Debug.WriteLine($"SpellLoader: Failed to load SpellTable for SpellSets.");
                  itemsSource = null;
             }
             return itemsSource;
        }
    }
} 