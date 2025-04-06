using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.UI;
using Windows.UI.Text;
using Windows.UI; // For Color class
using ACME.Managers;
using DatReaderWriter.Types;
using DatReaderWriter.Enums;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.ObjectModel;
using System.Globalization;
using DatReaderWriter; // Required for DatDatabase, SpellTable etc.
using ACME.Constants; // Required for DatFileIds
using Microsoft.UI.Text; // For FontWeights
using DatReaderWriter.DBObjs; // Added for SpellTable

namespace ACME.Renderers
{
    /// <summary>
    /// Custom renderer for SpellSet objects with editing functionality
    /// </summary>
    public class SpellSetRenderer : IObjectRenderer
    {
        private readonly GenericObjectRenderer _genericRenderer = new GenericObjectRenderer();

        /// <summary>
        /// Represents a tier and its associated spells for UI binding.
        /// </summary>
        private class TierViewModel
        {
            public uint Tier { get; set; }
            public ObservableCollection<uint> Spells { get; set; } = new ObservableCollection<uint>();
            public string SpellsAsString // Helper for easier editing in a TextBox
            {
                get => string.Join(", ", Spells);
                set
                {
                    Spells.Clear();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        var parts = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        foreach (var part in parts)
                        {
                            if (uint.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint spellId))
                            {
                                Spells.Add(spellId);
                            }
                            // Optionally add error handling for non-uint parts
                        }
                    }
                }
            }
        }

        public void Render(Panel targetPanel, object data, Dictionary<string, object>? context)
        {
            if (!(data is SpellSet spellSet))
            {
                // Fallback to generic renderer if not a SpellSet
                _genericRenderer.Render(targetPanel, data, context);
                return;
            }

            // Create a container for our custom controls
            var controlsPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };

            // Create Edit button
            var editButton = new Button
            {
                Content = "Edit SpellSet",
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 5, 0, 10)
            };

            // Add button click handler (will call ShowSpellSetEditDialog later)
            editButton.Click += async (sender, args) =>
            {
                // Show edit dialog with the spell set data
                 await ShowSpellSetEditDialog(spellSet, targetPanel.XamlRoot, context);
            };

            controlsPanel.Children.Add(editButton);

            // Add separator line
            var separator = new Rectangle
            {
                Height = 1,
                Fill = new SolidColorBrush(Colors.LightGray),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 5, 0, 10)
            };

            controlsPanel.Children.Add(separator);

            // Add the controls panel to the target panel
            targetPanel.Children.Add(controlsPanel);

            // Use the generic renderer to display the rest of the properties (the SpellSetTiers dictionary)
            _genericRenderer.Render(targetPanel, data, context);
        }

        /// <summary>
        /// Shows a dialog for editing SpellSet properties (tiers and spell lists)
        /// </summary>
        private async Task ShowSpellSetEditDialog(SpellSet spellSet, XamlRoot xamlRoot, Dictionary<string, object>? context = null)
        {
            EquipmentSet equipmentSetKey = default;
            bool foundKey = false;

            // 1. Get EquipmentSet key from context or by searching the SpellTable
            if (context != null && context.TryGetValue("SelectedItemId", out var selectedItemIdObj) && selectedItemIdObj is EquipmentSet eqKey)
            {
                equipmentSetKey = eqKey;
                foundKey = true;
                Debug.WriteLine($"Found EquipmentSet key '{equipmentSetKey}' from SelectedItemId context.");
            }
            else if (context != null && context.TryGetValue("DatabaseManager", out var dbMgrObj) && dbMgrObj is Managers.DatabaseManager dbMgr && dbMgr.CurrentDatabase != null)
            {
                var db = dbMgr.CurrentDatabase;
                if (db.TryReadFile<DatReaderWriter.DBObjs.SpellTable>(ACME.Constants.DatFileIds.SpellTableId, out var spellTable))
                {
                    foreach (var kvp in spellTable.SpellsSets)
                    {
                        if (object.ReferenceEquals(kvp.Value, spellSet))
                        {
                            equipmentSetKey = kvp.Key;
                            foundKey = true;
                            Debug.WriteLine($"Found EquipmentSet key '{equipmentSetKey}' via reference equality search.");
                            break;
                        }
                    }
                }
            }

            if (!foundKey)
            {
                Debug.WriteLine("Error: Could not determine the EquipmentSet key for the selected SpellSet.");
                var errorDialog = new ContentDialog
                {
                    Title = "Error",
                    Content = "Could not determine the Equipment Set identifier for this entry. Cannot edit.",
                    CloseButtonText = "OK",
                    XamlRoot = xamlRoot
                };
                await errorDialog.ShowAsync();
                return;
            }

            // 2. & 3. Create ContentDialog UI and Populate
            var mainPanel = new StackPanel { Spacing = 10 };

            // Display EquipmentSet (read-only)
            mainPanel.Children.Add(new TextBlock { 
                Text = $"Equipment Set: {equipmentSetKey} ({(int)equipmentSetKey})", 
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 10) 
            });

            // Create a list view model for tiers
            var tierViewModels = new ObservableCollection<TierViewModel>(
                spellSet.SpellSetTiers
                    .OrderBy(kvp => kvp.Key) // Ensure tiers are ordered
                    .Select(kvp => new TierViewModel 
                    {
                         Tier = kvp.Key, 
                         Spells = new ObservableCollection<uint>(kvp.Value.Spells) 
                    })
            );

            // ListView to display tiers and their spells
            var tiersListView = new ListView
            {
                ItemsSource = tierViewModels,
                SelectionMode = ListViewSelectionMode.Single,
                MaxHeight = 400 // Limit height to prevent overly large dialog
            };

            tiersListView.ItemTemplate = (DataTemplate)Microsoft.UI.Xaml.Markup.XamlReader.Load(@"
                <DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>
                    <StackPanel Orientation='Horizontal' Spacing='10'>
                        <TextBlock Text='{Binding Tier}' FontWeight='Bold' VerticalAlignment='Center' MinWidth='50'/>
                        <TextBox Text='{Binding SpellsAsString, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}' 
                                 Header='Spell IDs (comma-separated)' 
                                 MinWidth='400' 
                                 AcceptsReturn='False'/>
                    </StackPanel>
                </DataTemplate>");

            mainPanel.Children.Add(tiersListView);

            // Buttons for adding/removing tiers
            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Margin = new Thickness(0, 10, 0, 0) };
            var addTierButton = new Button { Content = "Add Tier" };
            var removeTierButton = new Button { Content = "Remove Selected Tier" };
            var newTierNumberBox = new NumberBox { 
                Header = "New Tier #", 
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact, 
                Minimum = 0, 
                Value = double.NaN // Use NaN for placeholder
            };

            addTierButton.Click += (s, e) =>
            {
                if (!double.IsNaN(newTierNumberBox.Value))
                {
                    uint newTier = (uint)newTierNumberBox.Value;
                    if (!tierViewModels.Any(tvm => tvm.Tier == newTier))
                    {
                        tierViewModels.Add(new TierViewModel { Tier = newTier });
                        // Sort after adding if desired
                         tierViewModels = new ObservableCollection<TierViewModel>(tierViewModels.OrderBy(t => t.Tier));
                         tiersListView.ItemsSource = tierViewModels; 
                         newTierNumberBox.Value = double.NaN; // Reset input
                    }
                    else
                    {
                        // Show error: Tier already exists
                        ShowSimpleDialog("Error", "A tier with this number already exists.", xamlRoot);
                    }
                }
                 else
                {
                     ShowSimpleDialog("Error", "Please enter a valid tier number.", xamlRoot);
                }
            };

            removeTierButton.Click += (s, e) =>
            {
                if (tiersListView.SelectedItem is TierViewModel selectedTier)
                {
                    tierViewModels.Remove(selectedTier);
                }
                else
                {
                    ShowSimpleDialog("Information", "Please select a tier to remove.", xamlRoot);
                }
            };

            buttonPanel.Children.Add(newTierNumberBox);
            buttonPanel.Children.Add(addTierButton);
            buttonPanel.Children.Add(removeTierButton);
            mainPanel.Children.Add(buttonPanel);

            var scrollViewer = new ScrollViewer
            {
                Content = mainPanel,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };

            // Create dialog
            var dialog = new ContentDialog
            {
                Title = "Edit SpellSet",
                Content = scrollViewer,
                PrimaryButtonText = "Save",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = xamlRoot,
                MinWidth = 600 // Set a reasonable minimum width
            };

            // Show dialog and handle result
            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                // 4. On Save Logic
                Debug.WriteLine("Save button clicked. Preparing to update SpellSet...");

                // Reconstruct the SpellSetTiers dictionary from the view model
                var updatedTiers = new Dictionary<uint, SpellSetTiers>();
                foreach (var tvm in tierViewModels)
                {
                    // Need to ensure the SpellsAsString setter has updated the Spells collection
                    // (Should happen due to TwoWay binding UpdateSourceTrigger=PropertyChanged)
                    updatedTiers[tvm.Tier] = new SpellSetTiers { Spells = new List<uint>(tvm.Spells) };
                    Debug.WriteLine($"Tier {tvm.Tier}: {string.Join(',', tvm.Spells)}");
                }

                 // Apply the changes to the original spellSet object
                spellSet.SpellSetTiers = updatedTiers;
                Debug.WriteLine($"Updated spellSet object in memory. EquipmentSet Key: {equipmentSetKey}");

                // --- Save to Database Logic ---
                try
                {
                    // a. Get DatabaseManager from context
                    if (context != null && context.TryGetValue("DatabaseManager", out var dbManagerObj) &&
                        dbManagerObj is DatabaseManager dbManager && dbManager.CurrentDatabase != null)
                    {
                        var db = dbManager.CurrentDatabase;

                        // b. Load SpellTable
                        if (db.TryReadFile<SpellTable>(DatFileIds.SpellTableId, out var spellTable) && spellTable != null)
                        {
                            // c. Update spellTable.SpellsSets[equipmentSetKey] = spellSet
                            if (spellTable.SpellsSets.ContainsKey(equipmentSetKey))
                            {
                                spellTable.SpellsSets[equipmentSetKey] = spellSet;
                                Debug.WriteLine($"Updated SpellSet for key {equipmentSetKey} in SpellTable object.");

                                // d. Save SpellTable
                                bool success = db.TryWriteFile(spellTable);

                                if (success)
                                {
                                    // e. Show confirmation
                                    Debug.WriteLine($"Successfully saved SpellTable with updated SpellSet for key {equipmentSetKey}.");
                                    await ShowSimpleDialog("SpellSet Updated", "The SpellSet has been updated successfully and saved to the database.", xamlRoot);

                                    // f. Refresh UI
                                    if (context.TryGetValue("RefreshDetailView", out var refreshAction) && refreshAction is Action<object> refresh)
                                    {
                                        refresh(spellSet); // Refresh with the updated object
                                    }
                                    else if (context.TryGetValue("RefreshView", out var refreshViewAction) && refreshViewAction is Action refreshView)
                                    {
                                        refreshView(); // Fallback to general view refresh
                                    }
                                }
                                else
                                {
                                    // Error saving
                                    Debug.WriteLine($"Error: Failed to save SpellTable for SpellSet key {equipmentSetKey}.");
                                    await ShowSimpleDialog("Error Saving Changes", "The SpellSet was updated in memory but couldn't be saved to the database file.", xamlRoot);
                                }
                            }
                            else
                            {
                                // Key not found
                                Debug.WriteLine($"Error: Could not find EquipmentSet key {equipmentSetKey} in SpellTable.SpellsSets.");
                                await ShowSimpleDialog("Error Saving Changes", $"The specified EquipmentSet key ({equipmentSetKey}) couldn't be found in the SpellTable. Changes were made in memory only.", xamlRoot);
                            }
                        }
                        else
                        {
                            // SpellTable load failed
                            Debug.WriteLine($"Error: Failed to load SpellTable (ID: {DatFileIds.SpellTableId}).");
                            await ShowSimpleDialog("Error Saving Changes", "The SpellTable couldn't be loaded from the database. Changes were made in memory only.", xamlRoot);
                        }
                    }
                    else
                    {
                        // DatabaseManager not found or no active DB
                        Debug.WriteLine("Warning: DatabaseManager not found in context or no active database.");
                        await ShowSimpleDialog("Warning", "SpellSet was updated in memory but changes may not be saved permanently because the database manager couldn't be accessed or no database is active.", xamlRoot);
                    }
                }
                catch (Exception ex)
                {
                    // General exception during save
                    Debug.WriteLine($"Exception during SpellSet save: {ex}");
                    await ShowSimpleDialog("Error Saving Changes", $"An unexpected error occurred while saving the SpellSet: {ex.Message}", xamlRoot);
                }
                // --- End Save to Database Logic ---
            }
        }

         /// <summary>
        /// Helper method to show a simple content dialog.
        /// </summary>
        private async Task ShowSimpleDialog(string title, string message, XamlRoot xamlRoot)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = "OK",
                XamlRoot = xamlRoot
            };
            await dialog.ShowAsync();
        }
    }
} 