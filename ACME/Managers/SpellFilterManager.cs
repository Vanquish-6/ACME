using DatReaderWriter.DBObjs;
using DatReaderWriter.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ACME.Managers
{
    /// <summary>
    /// Represents the result of a spell filtering operation.
    /// </summary>
    public class SpellFilterResult
    {
        public object? FilteredItemsSource { get; }
        public string StatusMessage { get; }
        public bool HasData { get; }

        public SpellFilterResult(object? itemsSource, string message, bool hasData)
        {
            FilteredItemsSource = itemsSource;
            StatusMessage = message;
            HasData = hasData;
        }
    }

    /// <summary>
    /// Manages the filtering of SpellTable data.
    /// </summary>
    public class SpellFilterManager
    {
        private SpellTable? _currentSpellTable;

        /// <summary>
        /// Sets the spell table to be used for filtering.
        /// </summary>
        /// <param name="spellTable">The spell table data.</param>
        public void SetSpellTable(SpellTable? spellTable)
        {
            _currentSpellTable = spellTable;
            Debug.WriteLine($"SpellFilterManager: SpellTable {(spellTable != null ? "set" : "cleared")}.");
        }

        /// <summary>
        /// Applies filters to the current SpellTable and returns the filtered items source and status message.
        /// </summary>
        /// <param name="nameFilter">The filter string for the spell name.</param>
        /// <returns>A SpellFilterResult containing the filtered data and status.</returns>
        public SpellFilterResult ApplyFilters(string nameFilter /* TODO: Add other filters as parameters */)
        {
            if (_currentSpellTable == null)
            {
                Debug.WriteLine("SpellFilterManager.ApplyFilters called, but no SpellTable is loaded.");
                return new SpellFilterResult(null, "No SpellTable loaded to filter.", false);
            }

            object? itemsSource = null;
            string statusMessage;
            MagicSchool? schoolFilter = null; // Placeholder
            uint? componentFilter = null; // Placeholder

            if (_currentSpellTable.Spells != null && _currentSpellTable.Spells.Count > 0)
            {
                // --- Filtering Logic - Filter the Dictionary directly ---
                var filteredEntries = _currentSpellTable.Spells.AsEnumerable(); // Start with all KeyValuePairs

                if (!string.IsNullOrWhiteSpace(nameFilter))
                {
                    filteredEntries = filteredEntries.Where(kvp => kvp.Value.Name != null && kvp.Value.Name.Contains(nameFilter, StringComparison.OrdinalIgnoreCase));
                }
                if (schoolFilter.HasValue)
                {
                    filteredEntries = filteredEntries.Where(kvp => kvp.Value.School == schoolFilter.Value);
                }
                if (componentFilter.HasValue)
                {
                    filteredEntries = filteredEntries.Where(kvp => kvp.Value.Components != null && kvp.Value.Components.Contains(componentFilter.Value));
                }
                // --- End Filtering Logic ---

                // Convert filtered KeyValuePairs to list for the UI
                // No need for the costly FirstOrDefault lookup here anymore
                var filteredList = filteredEntries
                    .Select(kvp => new { Id = kvp.Key, Name = kvp.Value.Name ?? "?", DisplayText = $"{kvp.Key}: {kvp.Value.Name ?? "?"}", Value = kvp.Value })
                    .OrderBy(s => s.Id)
                    .ToList();

                itemsSource = filteredList;

                // Determine status message
                bool isFiltered = !string.IsNullOrWhiteSpace(nameFilter) || schoolFilter.HasValue || componentFilter.HasValue;
                if (isFiltered)
                {
                    statusMessage = $"Showing {filteredList.Count} matching spells.";
                }
                else
                {
                    // Include unfiltered count in initial message
                    statusMessage = $"Listing all {filteredList.Count} spells."; 
                }
                Debug.WriteLine($"SpellFilterManager.ApplyFilters: Name='{nameFilter}', Found={filteredList.Count}");
                return new SpellFilterResult(itemsSource, statusMessage + " Select one.", true);
            }
            else
            {
                statusMessage = "No spells found in the loaded SpellTable.";
                 Debug.WriteLine("SpellFilterManager.ApplyFilters: No spells found in table.");
                return new SpellFilterResult(null, statusMessage, false);
            }
        }

        /// <summary>
        /// Gets the total count of spells in the current table (unfiltered).
        /// </summary>
        public int GetTotalSpellCount()
        {
            return _currentSpellTable?.Spells?.Count ?? 0;
        }
    }
} 